namespace HttpCache.Extensions;

public static class HttpExtensions
{
    public static bool TryAddHeader(this HttpRequestMessage message, string header, string[]? values)
    {
        if (values == null)
            return false;
        
        if (message.Headers.TryAddWithoutValidation(header, values))
            return true;

        return message.Content?.Headers?.TryAddWithoutValidation(header, values) ?? false;
    }
}