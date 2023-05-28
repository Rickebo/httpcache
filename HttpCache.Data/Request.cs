using System.Text.Json.Serialization;

namespace HttpCache.Data;

public class Request : HttpMessage
{
    public string? Url { get; set; }
    
    public string? Method { get; set; }
}