namespace HttpCache.Settings;

public class PulsarSettings : ISettings
{
    public const string Name = "Pulsar";
    public string SectionName => Name;

    public static PulsarSettings Default { get; } = new();

    public bool Enabled { get; set; } = true;
    public int Concurrency { get; set; } = 1;
    
    
    public string Url { get; set; } = "pulsar://localhost:6650";
    public string InputTopic { get; set; } = "non-persistent://public/default/http-cache-input";
    public string OutputTopic { get; set; } = "non-persistent://public/default/http-cache-output";
}