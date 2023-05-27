using System.Text.Json;
using HttpCache.Data;

namespace HttpCache.Database;

public interface ICacheDatabase
{
    public Task<Response?> TryGetValue(string key);
    public Task SetValue(string key, Response response, TimeSpan? maxAge);
    public Task<string> SerializeKey(HttpRequestMessage message, JsonSerializerOptions? jsonOptions = null);
}