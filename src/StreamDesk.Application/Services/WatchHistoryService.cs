using System;
using System.Collections.Generic;
using StreamDesk.Core;
using StreamDesk.Infrastructure.Database;

namespace StreamDesk.Application.Services;

/// <summary>Watch history service over the SQLite watch history store.</summary>
public sealed class WatchHistoryService : IWatchHistoryService
{
    private readonly WatchHistoryRepository _repository;

    public WatchHistoryService(WatchHistoryRepository repository)
    {
        _repository = repository;
    }

    public event Action? Changed;

    public IReadOnlyList<WatchHistoryEntry> GetContinueWatching(int limit = 20)
    {
        // Keep items that are started but not finished (or just finished).
        var entries = _repository.GetRecent(limit * 3);
        var result = new List<WatchHistoryEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.MediaType == MediaType.Channel)
            {
                continue;
            }

            var key = entry.ProviderId + "|" + entry.ItemId;
            if (!seen.Add(key))
            {
                continue;
            }

            if (entry.Completed && entry.PercentComplete < 2)
            {
                continue;
            }

            result.Add(entry);
            if (result.Count >= limit)
            {
                break;
            }
        }

        return result;
    }

    public WatchHistoryEntry? Get(string providerId, string itemId) => _repository.Get(providerId, itemId);

    public void RecordProgress(WatchHistoryEntry entry)
    {
        entry.UpdatedAtUtc = DateTime.UtcNow;
        _repository.Upsert(entry);
        Changed?.Invoke();
    }

    public void Remove(string providerId, string itemId) => _repository.Remove(providerId, itemId);
}
