using System.Net;
using System.Text;
using System.Text.Json;
using HttpCache.Data;

namespace HttpCache.Database;

public abstract class CacheDatabase : ICacheDatabase
{
    private static readonly HashSet<string> IgnoredHeaders = new()
    {
        "traceparent"
    };

    public abstract Task<Response?> TryGetValue(HttpRequestMessage request);
    public abstract Task SetValue(HttpRequestMessage request, Response response, TimeSpan? maxAge);

    protected async Task<string> SerializeMessage(HttpRequestMessage message, JsonSerializerOptions? options = null)
    {
        var dict = new Dictionary<string, object?>()
        {
            ["Content"] = await ReadContentAsBase64(message),
            ["Headers"] = message.Headers
                .Where(entry => !IgnoredHeaders.Contains(entry.Key))
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

        return JsonSerializer.Serialize(dict, options);
    }

    private async Task<string?> ReadContentAsBase64(HttpRequestMessage message)
    {
        var task = message?.Content?.ReadAsStreamAsync();

        if (task == null)
            return null;

        var contentStream = await task;

        return await ReadAsBase64(contentStream);
    }

    private async Task<string> ReadAsBase64(Stream stream)
    {
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var bytes = ms.ToArray();

        return Convert.ToBase64String(bytes);
    }

    public Response DeserializeResponse(string json) =>
        JsonSerializer.Deserialize<Response>(json) ??
        throw new ArgumentException("Cannot deserialize response.");

    public string SerializeResponse(Response response) =>
        JsonSerializer.Serialize(response);
}