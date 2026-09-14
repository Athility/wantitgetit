using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using StreamDesk.Core;
using StreamDesk.Infrastructure.Database;

namespace StreamDesk.Application.Managers;

/// <summary>Options controlling the download pipeline.</summary>
public sealed record DownloadManagerOptions
{
    public string DownloadDirectory { get; init; } = string.Empty;

    public int MaxConcurrentDownloads { get; init; } = 2;

    public bool AllowResume { get; init; } = true;
}

/// <summary>
/// Persistent download queue with pause/resume/cancel/retry, HTTP range resume
/// and restart recovery. Only sources resolved by an authorized provider reach
/// this pipeline; the manager never scrapes or extracts streams.
/// </summary>
public sealed class DownloadManager : IAsyncDisposable
{
    private static readonly TimeSpan ProgressSaveInterval = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<string, DownloadTask> _tasks = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new();
    private readonly ConcurrentDictionary<string, byte> _paused = new();
    private readonly ConcurrentDictionary<string, byte> _running = new();
    private readonly SemaphoreSlim _slots = new(2, 2);
    private readonly DownloadsRepository _repository;
    private readonly Func<DownloadManagerOptions> _options;
    private readonly Func<HttpClient> _httpClientFactory;
    private readonly Timer _persistTimer;

    public DownloadManager(DownloadsRepository repository, Func<DownloadManagerOptions> options, Func<HttpClient> httpClientFactory)
    {
        _repository = repository;
        _options = options;
        _httpClientFactory = httpClientFactory;
        RestorePersistedTasks();
        _persistTimer = new Timer(_ => PersistProgress(), null, ProgressSaveInterval, ProgressSaveInterval);
    }

    public event Action? Changed;

    public IReadOnlyList<DownloadTask> Tasks =>
        _tasks.Values.OrderBy(t => t.CreatedAtUtc).ToList();

    private void RestorePersistedTasks()
    {
        foreach (var task in _repository.GetAll())
        {
            if (task.Status == DownloadStatus.Downloading)
            {
                // Interrupted by restart: becomes paused so the user can resume.
                task.Status = DownloadStatus.Paused;
                _paused[task.Id] = 1;
                _repository.Upsert(task);
            }

            _tasks[task.Id] = task;
        }
    }

    public DownloadTask Enqueue(string itemId, string providerId, string title, string? subLabel, string url, string? destinationOverride = null, long? totalBytes = null)
    {
        var activeDuplicate = _tasks.Values.FirstOrDefault(t =>
            t.ItemId == itemId
            && t.ProviderId == providerId
            && t.Status is DownloadStatus.Queued or DownloadStatus.Downloading or DownloadStatus.Paused);
        if (activeDuplicate is not null)
        {
            throw new InvalidOperationException($"'{title}' is already in the download queue.");
        }

        var options = _options();
        var fileName = destinationOverride ?? SanitizeFileName(title);
        var destination = Path.Combine(options.DownloadDirectory, fileName);
        var task = new DownloadTask
        {
            ItemId = itemId,
            ProviderId = providerId,
            Title = title,
            SubLabel = subLabel,
            Url = url,
            DestinationPath = destination,
            TotalBytes = totalBytes,
            Status = DownloadStatus.Queued
        };
        _tasks[task.Id] = task;
        _repository.Upsert(task);
        Changed?.Invoke();
        Pump();
        return task;
    }

    public void Pause(string id)
    {
        if (_tasks.TryGetValue(id, out var task) && task.Status is DownloadStatus.Downloading or DownloadStatus.Queued)
        {
            _paused[id] = 1;
            task.Status = DownloadStatus.Paused;
            _repository.Upsert(task);
            Changed?.Invoke();
        }
    }

    public void Resume(string id)
    {
        if (_tasks.TryGetValue(id, out var task) && task.Status is DownloadStatus.Paused or DownloadStatus.Failed or DownloadStatus.Cancelled)
        {
            _paused.TryRemove(id, out _);
            task.Status = DownloadStatus.Queued;
            task.ErrorMessage = null;
            _repository.Upsert(task);
            Changed?.Invoke();
            Pump();
        }
    }

    public void Cancel(string id)
    {
        if (_tasks.TryGetValue(id, out var task))
        {
            _paused.TryRemove(id, out _);
            if (_cancellations.TryGetValue(id, out var cts))
            {
                cts.Cancel();
            }

            task.Status = DownloadStatus.Cancelled;
            _repository.Upsert(task);
            Changed?.Invoke();
        }
    }

    public void Retry(string id) => Resume(id);

    public void Delete(string id)
    {
        Cancel(id);
        if (_tasks.TryRemove(id, out _))
        {
            _cancellations.TryRemove(id, out var cts);
            cts?.Dispose();
            _repository.Remove(id);
            Changed?.Invoke();
        }
    }

    private void Pump()
    {
        var max = Math.Max(1, _options().MaxConcurrentDownloads);
        while (_running.Count < max)
        {
            var next = _tasks.Values
                .Where(t => t.Status == DownloadStatus.Queued && !_running.ContainsKey(t.Id))
                .MinBy(t => t.CreatedAtUtc);
            if (next is null)
            {
                return;
            }

            Start(next);
        }
    }

    private void Start(DownloadTask task)
    {
        _running[task.Id] = 1;
        var cts = new CancellationTokenSource();
        _cancellations[task.Id] = cts;
        _ = Task.Run(() => RunAsync(task, cts.Token));
    }

    private async Task RunAsync(DownloadTask task, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(task.DestinationPath)!);
            task.Status = DownloadStatus.Downloading;
            _repository.Upsert(task);
            Changed?.Invoke();

            using var client = _httpClientFactory();
            var resumeFrom = _options().AllowResume && File.Exists(task.DestinationPath)
                ? new FileInfo(task.DestinationPath).Length
                : 0;

            using var request = new HttpRequestMessage(HttpMethod.Get, task.Url);
            if (resumeFrom > 0)
            {
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(resumeFrom, null);
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}.");
            }

            if (task.TotalBytes is null && response.Content.Headers.ContentLength is { } length)
            {
                task.TotalBytes = resumeFrom > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent
                    ? length + resumeFrom
                    : length;
            }

            var append = resumeFrom > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent;
            task.BytesDownloaded = append ? resumeFrom : 0;
            if (!append)
            {
                task.TotalBytes = response.Content.Headers.ContentLength;
            }

            await using var network = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var file = new FileStream(task.DestinationPath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            var stopwatch = Stopwatch.StartNew();
            var lastSave = DateTime.UtcNow;
            var lastBytes = task.BytesDownloaded;
            int read;
            while ((read = await network.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (_paused.ContainsKey(task.Id))
                {
                    task.Status = DownloadStatus.Paused;
                    _repository.Upsert(task);
                    Changed?.Invoke();
                    return;
                }

                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                task.BytesDownloaded += read;

                if (DateTime.UtcNow - lastSave > ProgressSaveInterval)
                {
                    var elapsed = stopwatch.Elapsed.TotalSeconds;
                    task.SpeedBytesPerSecond = elapsed > 0.5 ? (task.BytesDownloaded - lastBytes) / elapsed : task.SpeedBytesPerSecond;
                    lastBytes = task.BytesDownloaded;
                    stopwatch.Restart();
                    _repository.Upsert(task);
                    lastSave = DateTime.UtcNow;
                    Changed?.Invoke();
                }
            }

            task.Status = DownloadStatus.Completed;
            task.SpeedBytesPerSecond = 0;
            task.ErrorMessage = null;
            if (task.TotalBytes is null || task.BytesDownloaded > task.TotalBytes)
            {
                task.TotalBytes = task.BytesDownloaded;
            }

            _repository.Upsert(task);
            Changed?.Invoke();
        }
        catch (OperationCanceledException)
        {
            task.Status = DownloadStatus.Cancelled;
            _repository.Upsert(task);
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            task.Status = DownloadStatus.Failed;
            task.ErrorMessage = ex switch
            {
                HttpRequestException => "The download failed due to a network error.",
                IOException => "The download failed while writing to disk.",
                _ => "The download failed unexpectedly."
            };
            _repository.Upsert(task);
            Changed?.Invoke();
        }
        finally
        {
            _running.TryRemove(task.Id, out _);
            if (_cancellations.TryRemove(task.Id, out var cts))
            {
                cts.Dispose();
            }

            Pump();
        }
    }

    private void PersistProgress()
    {
        foreach (var task in _tasks.Values)
        {
            if (task.Status == DownloadStatus.Downloading)
            {
                _repository.Upsert(task);
            }
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name;
    }

    public async ValueTask DisposeAsync()
    {
        await _persistTimer.DisposeAsync().ConfigureAwait(false);
        foreach (var cts in _cancellations.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _slots.Dispose();
    }
}
