namespace HttpCache.Settings;

public class RedisSettings : ISettings
{
    public const string Name = "Redis";
    public string SectionName => Name;

    /// <summary>
    /// A list of Redis endpoints to connect to.
    /// </summary>
    public List<string> EndPoints { get; set; } = new();
}