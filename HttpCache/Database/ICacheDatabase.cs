using HttpCache.Data;

namespace HttpCache.Database;

public interface ICacheDatabase
{
    public Task<Response?> TryGetValue(HttpRequestMessage request);
    public Task SetValue(HttpRequestMessage request, Response response, TimeSpan? maxAge);
}