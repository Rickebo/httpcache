using System.Net;
using HttpCache.Data;
using HttpCache.Database;
using HttpCache.Extensions;
using HttpCache.Settings;

namespace HttpCache.Services;

public class RequestHandler
{
    private readonly ILogger<RequestHandler> _logger;
    private readonly ICacheDatabase _database;
    private readonly HttpSettings _settings;
    private readonly HttpClient _client = new();

    public RequestHandler(
        ILogger<RequestHandler> logger,
        ICacheDatabase database,
        HttpSettings settings
    )
    {
        _logger = logger;
        _database = database;
        _settings = settings;
        _client.DefaultRequestHeaders.Clear();
    }

    public async Task<CacheResponse> HandleMessage(
        Request request,
        PerformanceMonitor monitor,
        TimeSpan? maxAge = null
    ) => await HandleMessage(
        ConstructMessage(request),
        monitor,
        maxAge
    );

    public async Task<CacheResponse> HandleMessage(
        HttpRequestMessage message,
        PerformanceMonitor monitor,
        TimeSpan? maxAge = null
    )
    {
        var messageKey = await _database.SerializeKey(message);

        var cachedResponse = await monitor.RunTimedAsync(
            Constants.DatabaseTime,
            () => _database.TryGetValue(messageKey)
        );

        if (cachedResponse != null)
            return new CacheResponse(
                Response: cachedResponse,
                IsCached: true
            );

        var responseMessage = await _client.SendAsync(message);
        var response = await ConstructResponse(responseMessage);

        await monitor.RunTimedAsync(
            Constants.DatabaseTime,
            () => _database.SetValue(messageKey, response, maxAge)
        );

        return new CacheResponse(
            Response: response,
            IsCached: false
        );
    }

    private HttpRequestMessage ConstructMessage(Request request, bool uriIsHost = true)
    {
        var method = request.Method;
        var headers = request.Headers;

        var actualMethod = new HttpMethod(method);

        var message = new HttpRequestMessage(actualMethod, request.Url)
        {
            Content = request.Content != null
                ? new ByteArrayContent(request.Content)
                : null
        };

        if (message.Content != null && request.ContentHeaders != null)
            foreach (var entry in request.ContentHeaders)
            {
                var name = entry.Key;
                var values = entry.Value;

                if (name == _settings.HostHeader || name == "Host")
                    continue;

                message.Content.Headers.TryAddWithoutValidation(name, (IEnumerable<string?>)values);
            }

        foreach (var entry in headers)
        {
            var name = entry.Key;
            var values = entry.Value;

            if (name == _settings.HostHeader || name == "Host")
                continue;

            message.TryAddHeader(name, values);
        }

        return message;
    }

    private async Task<Response> ConstructResponse(HttpResponseMessage responseMessage)
    {
        var contentStream = await responseMessage.Content.ReadAsStreamAsync();
        var ms = new MemoryStream();

        await contentStream.CopyToAsync(ms);

        return new Response()
        {
            Content = ms.ToArray(),
            StatusCode = responseMessage.StatusCode,
            Headers = responseMessage.Headers.ToDictionary(
                x => x.Key,
                x => x.Value.ToArray()
            ),
            ContentHeaders = responseMessage.Content?.Headers?.ToDictionary(
                x => x.Key,
                x => x.Value.ToArray()
            ),
            TrailingHeaders = responseMessage.TrailingHeaders?.ToDictionary(
                x => x.Key,
                x => x.Value.ToArray()
            ) ?? new()
        };
    }

    public record CacheResponse(Response Response, bool IsCached);
}