using System.Text.Json;
using System.Text.Json.Serialization;

namespace HttpCache;

public class JsonByteArrayBase64Converter : JsonConverter<byte[]?>
{
    public override byte[]? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var stringValue = reader.GetString();

        return stringValue != null
            ? Convert.FromBase64String(stringValue)
            : null;
    }

    public override void Write(
        Utf8JsonWriter writer,
        byte[]? value,
        JsonSerializerOptions options
    )
    {
        if (value == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(Convert.ToBase64String(value));
    }
}