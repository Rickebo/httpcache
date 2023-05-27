using System.Data.SqlTypes;

namespace HttpCache.Settings;

public class HttpAccessSettings
{
    public static HttpAccessSettings Default { get; } = new();
    
    /// <summary>
    /// Whether or not hosts specified hosts should be blacklisted. If true, accesses to the blacklisted hosts will
    /// fail.
    /// </summary>
    public bool BlacklistHosts { get; set; } = false;
    
    /// <summary>
    /// Whether or not IP addresses should be blacklisted. If true, accesses to the blacklisted IP addresses will fail.
    /// </summary>
    public bool BlacklistIp { get; set; } = true;
    
    /// <summary>
    /// Whether or not to whitelist hosts. If true, accesses to hosts not specified in the host whitelist will fail.
    /// </summary>
    public bool WhitelistHosts { get; set; } = true;
    
    /// <summary>
    /// Whether or not to whitelist IP addresses. If true, accesses to IP addresses not specified in the IP whitelist
    /// will fail. Note that all IP addresses for a host must be whitelisted for the host to be whitelisted, not just
    /// one.
    /// </summary>
    public bool WhitelistIp { get; set; } = false;
}