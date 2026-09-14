using System;

namespace StreamDesk.App;

/// <summary>
/// Minimal service locator used as the app's composition root. Small, explicit
/// and synchronous; a full DI container is unnecessary for this size of app.
/// </summary>
public sealed class SimpleContainer
{
    private readonly System.Collections.Generic.Dictionary<Type, object> _instances = new();

    public SimpleContainer Register<T>(T instance) where T : class
    {
        _instances[typeof(T)] = instance;
        return this;
    }

    public T? GetService<T>() where T : class =>
        _instances.TryGetValue(typeof(T), out var instance) ? (T)instance : null;

    public T GetRequiredService<T>() where T : class =>
        (T)(_instances.TryGetValue(typeof(T), out var instance)
            ? instance
            : throw new InvalidOperationException($"Service not registered: {typeof(T).Name}"));
}
