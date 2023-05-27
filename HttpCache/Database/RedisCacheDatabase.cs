using HttpCache.Data;
using HttpCache.Settings;
using StackExchange.Redis;

namespace HttpCache.Database;

public class RedisCacheDatabase : CacheDatabase
{
    private readonly RedisSettings _settings;
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public RedisCacheDatabase(CacheSettings cacheSettings, RedisSettings settings)
        : base(cacheSettings)
    {
        _settings = settings;
        _redis = ConnectionMultiplexer.Connect(
            new ConfigurationOptions
            {
                EndPoints = new EndPointCollection(_settings
                    .EndPoints
                    .Select(ep =>
                        EndPointCollection.TryParse(ep) ??
                        throw new Exception("Cannot parse endpoints: at least one is invalid.")
                    )
                    .ToList()
                )
            }
        );

        _db = _redis.GetDatabase();
    }

    public override async Task<Response?> TryGetValue(
        string key
    )
    {
        var serializedResponse = await _db.StringGetAsync(key);
        return !serializedResponse.IsNullOrEmpty
            ? DeserializeResponse(serializedResponse.ToString())
            : null;
    }

    public override async Task SetValue(
        string key,
        Response response,
        TimeSpan? maxAge
    )
    {
        var value = SerializeResponse(response);
        
        await _db.StringSetAsync(
            key: new RedisKey(key),
            value: new RedisValue(value),
            expiry: maxAge ?? CacheSettings.DefaultMaxAge
        );
    }

    private record CacheResultContainer(DateTime CacheTime, Response Data)
    {
        
    }
}