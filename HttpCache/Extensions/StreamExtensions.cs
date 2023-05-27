namespace HttpCache.Extensions;

public static class StreamExtensions
{
    public static async Task<string> ToBase64(this Stream stream)
    {
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var bytes = ms.ToArray();

        return Convert.ToBase64String(bytes);
    }

    public static Stream FromBase64(string text)
    {
        var bytes = Convert.FromBase64String(text);
        var ms = new MemoryStream(bytes);

        return ms;
    }
}