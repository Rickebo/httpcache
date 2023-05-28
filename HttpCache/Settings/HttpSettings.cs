using System.Net;
using System.Text.Json.Serialization;
using NetTools;

namespace HttpCache.Settings;

public class HttpSettings : ISettings
{
    public const string Name = "Http";
    public string SectionName => Name;

    public static HttpSettings Default { get; } = new ();
    
    
    public string HostHeader { get; set; } = "Actual-Host";
    
    /// <summary>
    /// Access settings specifying how whitelist/blacklist should be handled.
    /// </summary>
    public HttpAccessSettings Access { get; set; } = HttpAccessSettings.Default;

    /// <summary>
    /// Set of IP ranges to blacklist if access settings specify that IP blacklisting should be used.
    /// </summary>
    public HashSet<string> BlacklistedIp { get; set; } = new()
    {
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
        "127.0.0.0/8",
        "169.254.0.0/16",
        "224.0.0.0/4",
        "::1/128",
        "::/128",
        "fc00::/7",
        "fe80::/10"
    };

    /// <summary>
    /// List of headers removed from proxied result before returning to client. Useful to remove headers that would,
    /// for example, interfere with how the proxy responds to requests.
    /// </summary>
    public HashSet<string> FilteredHeaders { get; set; } = 
        new(StringComparer.OrdinalIgnoreCase)
    {
        "Transfer-Encoding"
    };


    /// <summary>
    /// Set of hostnames to blacklist if access settings specify that host blacklisting should be used.
    /// </summary>
    public HashSet<string> BlacklistedHost { get; set; } = new();

    /// <summary>
    /// Set of IP ranges to whitelist if access settings specify that IP whitelisting should be used.
    /// </summary>
    public HashSet<string> WhitelistedIp { get; set; } = new();

    /// <summary>
    /// Set of hostnames to whitelist if access settings specify that host whitelisting should be used.
    /// </summary>
    public HashSet<string> WhitelistedHost { get; set; } = new();

    public HashSet<IPAddressRange> GetBlacklistedRanges() => 
        BlacklistedIp
            .Select(range => IPAddressRange.Parse(range))
            .ToHashSet();

    public HashSet<IPAddressRange> GetWhitelistedRanges() =>
        WhitelistedIp
            .Select(range => IPAddressRange.Parse(range))
            .ToHashSet();

    public HashSet<string> GetWhitelistedHosts() =>
        new HashSet<string>(WhitelistedHost, StringComparer.OrdinalIgnoreCase);

    public HashSet<string> GetBlacklistedHosts() =>
        new HashSet<string>(BlacklistedHost, StringComparer.OrdinalIgnoreCase);
}