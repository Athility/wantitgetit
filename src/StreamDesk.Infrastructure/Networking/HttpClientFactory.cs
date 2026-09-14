using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace StreamDesk.Infrastructure.Networking;

/// <summary>Factory for hardened HttpClient instances used by providers and services.</summary>
public static class HttpClientFactory
{
    public static HttpClient Create(string? userAgent = null)
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            EnableMultipleHttp2Connections = true
        });
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent ?? $"StreamDesk/{Core.AppVersion.Informational}");
        return client;
    }
}
