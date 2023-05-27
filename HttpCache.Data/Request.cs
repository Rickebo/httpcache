using System.Text.Json.Serialization;

namespace HttpCache.Data;

public class Request : HttpMessage
{
    public string? Url { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public string? Method { get; set; }
}