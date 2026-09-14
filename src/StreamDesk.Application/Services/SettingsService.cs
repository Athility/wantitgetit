using System;
using StreamDesk.Core;
using StreamDesk.Infrastructure.Storage;

namespace StreamDesk.Application.Services;

/// <summary>Default settings service over the JSON settings store.</summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IAppSettingsStore _store;

    public SettingsService(IAppSettingsStore store)
    {
        _store = store;
        Current = new AppSettings();
    }

    public AppSettings Current { get; private set; }

    public event Action? Changed;

    public void Load()
    {
        Current = _store.Load();
        Changed?.Invoke();
    }

    public void Save()
    {
        _store.Save(Current);
        Changed?.Invoke();
    }

    public void ResetToDefaults()
    {
        Current = new AppSettings();
        _store.Save(Current);
        Changed?.Invoke();
    }
}
