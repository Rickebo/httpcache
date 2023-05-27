using System.Net;
using System.Text.Json.Serialization;

namespace HttpCache.Data;

public class Response
{
    public Dictionary<string, string[]> Headers { get; set; } = new();
    public Dictionary<string, string[]>? ContentHeaders { get; set; } = new();
    public Dictionary<string, string[]> TrailingHeaders { get; set; } = new();

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HttpStatusCode StatusCode { get; set; }
    
    [JsonConverter(typeof(JsonByteArrayBase64Converter))]
    public byte[]? Content { get; set; } = Array.Empty<byte>();
}