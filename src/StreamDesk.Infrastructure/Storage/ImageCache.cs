using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace StreamDesk.Infrastructure.Storage;

/// <summary>
/// Two-level image cache (memory + disk) for posters, backdrops and thumbnails.
/// Keeps large libraries out of RAM; entries evicted from memory stay on disk.
/// </summary>
public sealed class ImageCache
{
    private const int MaxMemoryEntries = 256;

    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly ConcurrentDictionary<string, byte[]> _memory = new();
    private readonly ConcurrentDictionary<string, Task<byte[]?>> _inFlight = new();

    public ImageCache(HttpClient httpClient, string cacheDirectory)
    {
        _httpClient = httpClient;
        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(cacheDirectory);
    }

    /// <summary>Cached image bytes for a URL; downloads on first request. Null on failure.</summary>
    public Task<byte[]?> GetBytesAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult<byte[]?>(null);
        }

        if (_memory.TryGetValue(url, out var cached))
        {
            return Task.FromResult<byte[]?>(cached);
        }

        return _inFlight.GetOrAdd(url, DownloadAsync);
    }

    /// <summary>True when the image is available from cache without network.</summary>
    public bool IsCachedLocally(string url) =>
        !string.IsNullOrWhiteSpace(url) && (File.Exists(PathFor(url)) || _memory.ContainsKey(url));

    public string PathFor(string url)
    {
        return System.IO.Path.Combine(_cacheDirectory, Hash(url) + ".img");
    }

    private async Task<byte[]?> DownloadAsync(string url)
    {
        try
        {
            var diskPath = PathFor(url);
            byte[] bytes;
            if (File.Exists(diskPath) && new FileInfo(diskPath).Length > 0)
            {
                bytes = await File.ReadAllBytesAsync(diskPath).ConfigureAwait(false);
            }
            else
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                await File.WriteAllBytesAsync(diskPath, bytes).ConfigureAwait(false);
            }

            _memory[url] = bytes;
            EvictIfNeeded();
            return bytes;
        }
        catch (Exception)
        {
            // Image caching must never break the UI; callers render a placeholder.
            return null;
        }
        finally
        {
            _inFlight.TryRemove(url, out _);
        }
    }

    private void EvictIfNeeded()
    {
        if (_memory.Count <= MaxMemoryEntries)
        {
            return;
        }

        foreach (var key in new List<string>(_memory.Keys))
        {
            if (_memory.Count <= MaxMemoryEntries)
            {
                break;
            }

            _memory.TryRemove(key, out _);
        }
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
