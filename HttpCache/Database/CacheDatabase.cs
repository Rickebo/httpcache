using System.Net;
using System.Text;
using System.Text.Json;
using HttpCache.Data;
using HttpCache.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace HttpCache.Database;

public abstract class CacheDatabase : ICacheDatabase
{
    protected CacheSettings CacheSettings { get; }
    private HashSet<string> _ignoredHeaders { get; }

    public CacheDatabase(CacheSettings settings)
    {
        CacheSettings = settings;
        _ignoredHeaders = settings.IgnoredHeaders;
    }

    public abstract Task<Response?> TryGetValue(string request);
    public abstract Task SetValue(string request, Response response, TimeSpan? maxAge);

    public async Task<string> SerializeKey(HttpRequestMessage message, JsonSerializerOptions? jsonOptions = null)
    {
        var dict = new Dictionary<string, object?>()
        {
            ["Content"] = await ReadContentAsBase64(message),
            ["Headers"] = message.Headers
                .Where(entry => !_ignoredHeaders.Contains(entry.Key))
                .ToDictionary(
                    x => x.Key,
                    x => x.Value
                ),
            ["Method"] = message.Method.ToString(),
            ["RequestUri"] = message.RequestUri?.ToString() ??
                             throw new ArgumentNullException(
                                 "Cannot serialize message: uri is null."
                             ),
            ["Version"] = message.Version.ToString()
        };

        return JsonSerializer.Serialize(dict, options: jsonOptions);
    }

    private async Task<string?> ReadContentAsBase64(HttpRequestMessage message)
    {
        var task = message?.Content?.ReadAsStreamAsync();

        if (task == null)
            return null;

        var contentStream = await task;

        return await contentStream.ToBase64();
    }

    public Response DeserializeResponse(string json) =>
        JsonSerializer.Deserialize<Response>(json) ??
        throw new ArgumentException("Cannot deserialize response.");

    public string SerializeResponse(Response response) =>
        JsonSerializer.Serialize(response);
}