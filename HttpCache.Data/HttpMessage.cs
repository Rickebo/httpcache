using System.Text.Json.Serialization;

namespace HttpCache.Data;

public class HttpMessage
{
    public Dictionary<string, string[]> Headers { get; set; } = new();
    public Dictionary<string, string[]>? ContentHeaders { get; set; } = new();
    public Dictionary<string, string[]> TrailingHeaders { get; set; } = new();

    [JsonConverter(typeof(JsonByteArrayBase64Converter))]
    public byte[]? Content { get; set; } = Array.Empty<byte>();
}