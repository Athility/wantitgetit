using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using StreamDesk.Application.Managers;
using StreamDesk.Core;

namespace StreamDesk.App.ViewModels;

/// <summary>Row VM for one download task.</summary>
public sealed class DownloadItemViewModel : ViewModelBase
{
    private readonly DownloadManager _manager;

    public DownloadItemViewModel(DownloadTask task, DownloadManager manager)
    {
        Task = task;
        _manager = manager;
    }

    public DownloadTask Task { get; }

    public string Id => Task.Id;

    public string Title => Task.Title;

    public string SubLabel => Task.SubLabel ?? string.Empty;

    public DownloadStatus Status => Task.Status;

    public string StatusLabel => Task.Status switch
    {
        DownloadStatus.Queued => "Queued",
        DownloadStatus.Downloading => "Downloading",
        DownloadStatus.Paused => "Paused",
        DownloadStatus.Completed => "Completed",
        DownloadStatus.Failed => "Failed",
        DownloadStatus.Cancelled => "Cancelled",
        _ => string.Empty
    };

    public double Percent => Task.TotalBytes is > 0
        ? Math.Min(100, Task.BytesDownloaded / (double)Task.TotalBytes * 100)
        : 0;

    public string PercentLabel => $"{Percent:0}%";

    public string Speed => Task.Status == DownloadStatus.Downloading && Task.SpeedBytesPerSecond > 0
        ? FormatBytes(Task.SpeedBytesPerSecond) + "/s"
        : string.Empty;

    public string Size => Task.TotalBytes is { } total
        ? $"{FormatBytes(Task.BytesDownloaded)} / {FormatBytes(total)}"
        : FormatBytes(Task.BytesDownloaded);

    public string Remaining => Task.Status == DownloadStatus.Downloading &&
                               Task.TotalBytes is { } total &&
                               Task.SpeedBytesPerSecond > 1024
        ? TimeSpan.FromSeconds(Math.Max(0, (total - Task.BytesDownloaded) / Task.SpeedBytesPerSecond)).ToString(@"mm\:ss")
        : string.Empty;

    public string? Error => Task.ErrorMessage;

    public bool CanPause => Status is DownloadStatus.Downloading or DownloadStatus.Queued;

    public bool CanResume => Status is DownloadStatus.Paused or DownloadStatus.Failed or DownloadStatus.Cancelled;

    public bool IsActive => Status is DownloadStatus.Downloading or DownloadStatus.Queued;

    public bool IsCompletedSection => Status == DownloadStatus.Completed;

    public void Refresh()
    {
        OnPropertyChanged(string.Empty);
    }

    private static string FormatBytes(double bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        var index = 0;
        while (bytes >= 1024 && index < units.Length - 1)
        {
            bytes /= 1024;
            index++;
        }

        return $"{bytes:0.#} {units[index]}";
    }
}

/// <summary>Downloads page state, grouped into ACTIVE / QUEUED / COMPLETED.</summary>
public sealed class DownloadsViewModel : ViewModelBase
{
    private readonly SimpleContainer _services;
    private readonly DownloadManager _manager;
    private IReadOnlyList<DownloadItemViewModel> _active = Array.Empty<DownloadItemViewModel>();
    private IReadOnlyList<DownloadItemViewModel> _queued = Array.Empty<DownloadItemViewModel>();
    private IReadOnlyList<DownloadItemViewModel> _completed = Array.Empty<DownloadItemViewModel>();
    private IReadOnlyList<DownloadItemViewModel> _failed = Array.Empty<DownloadItemViewModel>();

    public DownloadsViewModel(SimpleContainer services)
    {
        _services = services;
        _manager = services.GetRequiredService<DownloadManager>();
        _manager.Changed += Refresh;
    }

    public IReadOnlyList<DownloadItemViewModel> Active
    {
        get => _active;
        private set => SetProperty(ref _active, value);
    }

    public IReadOnlyList<DownloadItemViewModel> Queued
    {
        get => _queued;
        private set => SetProperty(ref _queued, value);
    }

    public IReadOnlyList<DownloadItemViewModel> Completed
    {
        get => _completed;
        private set => SetProperty(ref _completed, value);
    }

    public IReadOnlyList<DownloadItemViewModel> Failed
    {
        get => _failed;
        private set => SetProperty(ref _failed, value);
    }

    public bool HasAny => Active.Count + Queued.Count + Completed.Count + Failed.Count > 0;

    public bool HasNone => !HasAny;

    public bool HasActive => Active.Count > 0;

    public bool HasQueued => Queued.Count > 0;

    public bool HasCompleted => Completed.Count > 0;

    public bool HasFailed => Failed.Count > 0;

    public void Refresh()
    {
        var rows = _manager.Tasks.Select(t => new DownloadItemViewModel(t, _manager)).ToList();
        Active = rows.Where(r => r.Status == DownloadStatus.Downloading).ToList();
        Queued = rows.Where(r => r.Status is DownloadStatus.Queued or DownloadStatus.Paused).ToList();
        Completed = rows.Where(r => r.Status == DownloadStatus.Completed).ToList();
        Failed = rows.Where(r => r.Status is DownloadStatus.Failed or DownloadStatus.Cancelled).ToList();
        OnPropertyChanged(nameof(HasAny));
        OnPropertyChanged(nameof(HasNone));
        OnPropertyChanged(nameof(HasActive));
        OnPropertyChanged(nameof(HasQueued));
        OnPropertyChanged(nameof(HasCompleted));
        OnPropertyChanged(nameof(HasFailed));
    }

    public void Pause(string id) => _manager.Pause(id);

    public void Resume(string id) => _manager.Resume(id);

    public void Cancel(string id) => _manager.Cancel(id);

    public void Retry(string id) => _manager.Retry(id);

    public void Delete(string id) => _manager.Delete(id);
}
