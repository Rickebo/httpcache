using HttpCache.Settings;
using Microsoft.AspNetCore.Mvc.Formatters.Xml;

public class CacheSettings : ISettings
{
    public const string Name = "Cache";
    public string SectionName => Name;

    public static readonly CacheSettings Default = new();

    /// <summary>
    /// The default max age to keep an item in the cache for. If null, the max age will be determined by the caching
    /// database, and likely no expiry will be used.
    /// </summary>
    public TimeSpan? DefaultMaxAge { get; set; } = null;
    
    /// <summary>
    /// Headers to exclude when generating a cache key for a request. Hence, a header added to this list will not affect
    /// the cache key for a request where it is present. Useful to exclude headers with, for example, a unique value
    /// used to debug/trace the request, or to, for example, more efficiently cache requests with a unique valued header
    /// that still should be cached.
    /// </summary>
    public HashSet<string> IgnoredHeaders { get; set; } = new()
    {
        "traceparent"
    };

}