using System.Diagnostics;
using System.Net;
using System.Text;
using HttpCache.Data;
using HttpCache.Database;
using HttpCache.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using NetTools;
using HttpMethod = Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpMethod;

namespace HttpCache.Controllers;

public class HttpCacheController : ControllerBase
{
    private readonly ILogger<HttpCacheController> _logger;
    private readonly ICacheDatabase _database;
    private readonly HttpSettings _settings;
    private readonly HttpClient _client = new();

    private readonly HttpAccessSettings _accessSettings;

    private readonly HashSet<IPAddressRange> _blacklistedRanges;
    private readonly HashSet<IPAddressRange> _whitelistedRanges;

    private readonly HashSet<string> _blacklistedHosts;
    private readonly HashSet<string> _whitelistedHosts;

    public HttpCacheController(
        ILogger<HttpCacheController> logger,
        ICacheDatabase database,
        HttpSettings settings
    )
    {
        _logger = logger;
        _database = database;
        _settings = settings;
        _client.DefaultRequestHeaders.Clear();

        _accessSettings = _settings.Access;
        _blacklistedRanges = settings.GetBlacklistedRanges();
        _whitelistedRanges = settings.GetWhitelistedRanges();
        _blacklistedHosts = settings.GetBlacklistedHosts();
        _whitelistedHosts = settings.GetWhitelistedHosts();
    }

    public async Task HandleRequest(TimeSpan? maxAge = null)
    {
        var sw = new Stopwatch();
        var dbSw = new Stopwatch();

        sw.Start();

        if (!Validate(Request))
            return;

        var message = ConstructMessage(Request);

        dbSw.Start();
        var cachedResponse = await _database.TryGetValue(message);
        dbSw.Stop();

        if (cachedResponse != null)
        {
            await RespondWith(cachedResponse, Response);
            await Response.CompleteAsync();

            sw.Stop();
            dbSw.Stop();
            _logger.LogInformation(
                "Handled {Cached} request in {Elapsed} ms, with {ElapsedDb} ms of DB delay " +
                "with destination: {Destination}",
                "cached",
                sw.ElapsedMilliseconds,
                dbSw.ElapsedMilliseconds,
                message.RequestUri.ToString()
            );
            
            return;
        }

        var responseMessage = await _client.SendAsync(message);
        var response = await ConstructResponse(responseMessage);

        dbSw.Start();
        await _database.SetValue(message, response, maxAge);
        dbSw.Stop();

        await RespondWith(response, Response);
        sw.Stop();

        _logger.LogInformation(
            "Handled {Cached} request in {Elapsed} ms, with {ElapsedDb} ms of DB delay " +
            "with destination: {Destination}",
            "non-cached",
            sw.ElapsedMilliseconds,
            dbSw.ElapsedMilliseconds,
            message.RequestUri.ToString()
        );
    }

    private bool Validate(HttpRequest request)
    {
        var hostIps = GetActualHostIp(Request, out var host);
        var sourceIp = GetSourceIp(request);

        if (_accessSettings.BlacklistHosts && _blacklistedHosts.Contains(host))
        {
            _logger.LogWarning(
                "Blocked attempt by {SourceIp} to load {Destination} which is blacklisted.",
                sourceIp,
                host
            );

            Cancel(Response);
            return false;
        }

        if (_accessSettings.BlacklistIp)
            foreach (var ip in hostIps)
            {
                if (!_blacklistedRanges.Any(range => range.Contains(ip)))
                    continue;

                _logger.LogWarning(
                    "Blocked attempt by {SourceIp} to load {Destination} which resolves to blacklisted IP {Ip}",
                    sourceIp,
                    host,
                    ip
                );

                Cancel(Response);
                return false;
            }

        if (_accessSettings.WhitelistHosts && !_whitelistedHosts.Contains(host))
        {
            _logger.LogWarning(
                "Blocked attempt by {SourceIp} to load {Destination} which is not whitelisted.",
                sourceIp,
                host
            );

            Cancel(Response);
            return false;
        }

        if (_accessSettings.WhitelistIp)
            foreach (var ip in hostIps)
            {
                if (_whitelistedRanges.Any(range => range.Contains(ip)))
                    continue;

                _logger.LogWarning(
                    "Blocked attempt by {SourceIp} to load {Destination} which resolves to " +
                    "non-whitelisted IP {Ip}",
                    Request.HttpContext.Connection.RemoteIpAddress,
                    host,
                    ip
                );

                Cancel(Response);
                return false;
            }


        return true;
    }

    private IPAddress? GetSourceIp(HttpRequest request) =>
        request.HttpContext.Connection.RemoteIpAddress;

    private void Cancel(HttpResponse response)
    {
        response.StatusCode = 400;
        response.CompleteAsync();
    }

    private IPAddress[] GetActualHostIp(HttpRequest request, out string host)
    {
        var headers = request.Headers;
        var hostHeader = headers[_settings.HostHeader];

        if (hostHeader.Count != 1 || hostHeader[0] == null)
            throw new Exception("Invalid actual host header specified.");

        var actualHost = new Uri(hostHeader[0]!);
        host = actualHost.Host;
        return Dns.GetHostAddresses(host);
    }

    [NonAction]
    private HttpRequestMessage ConstructMessage(HttpRequest request)
    {
        var method = request.Method;
        var headers = request.Headers;
        var hostHeader = headers[_settings.HostHeader];

        if (hostHeader.Count != 1 || hostHeader[0] == null)
            throw new Exception("Invalid actual host header specified.");

        var actualHost = new Uri(hostHeader[0]!);
        var actualMethod = new System.Net.Http.HttpMethod(method);

        var message = new HttpRequestMessage(actualMethod, actualHost)
        {
            Version = request.Protocol switch
            {
                "HTTP/1.0" => HttpVersion.Version10,
                "HTTP/1.1" => HttpVersion.Version11,
                "HTTP/2.0" => HttpVersion.Version20,
                _ => throw new ArgumentOutOfRangeException()
            },
            Content = request.ContentType != null
                ? new StreamContent(request.Body)
                : null
        };

        foreach (var entry in headers)
        {
            var name = entry.Key;
            var values = entry.Value;

            if (name == _settings.HostHeader || name == "Host")
                continue;

            message.Headers.Add(name, (IEnumerable<string?>)values);
        }

        return message;
    }

    [NonAction]
    private async Task<Response> ConstructResponse(HttpResponseMessage responseMessage)
    {
        var contentStream = await responseMessage.Content.ReadAsStreamAsync();
        var ms = new MemoryStream();

        await contentStream.CopyToAsync(ms);

        return new Response()
        {
            Content = ms.ToArray(),
            StatusCode = (HttpStatusCode)responseMessage.StatusCode,
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

    [NonAction]
    private async Task RespondWith(Response message, HttpResponse response)
    {
        response.StatusCode = (int)message.StatusCode;

        foreach (var header in message.Headers)
            response.Headers.Add(
                header.Key,
                new StringValues(header.Value.ToArray())
            );

        if (message.ContentHeaders != null)
            foreach (var header in message.ContentHeaders)
                response.Headers.Add(
                    header.Key,
                    new StringValues(header.Value.ToArray())
                );

        await response.Body.WriteAsync(message.Content);

        foreach (var header in message.TrailingHeaders)
            response.AppendTrailer(
                header.Key,
                new StringValues(header.Value.ToArray())
            );
    }
}