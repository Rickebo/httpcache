using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web;
using HttpCache.Data;
using HttpCache.Database;
using HttpCache.Extensions;
using HttpCache.Services;
using HttpCache.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using NetTools;
using HttpMethod = Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpMethod;

namespace HttpCache.Controllers;

public class HttpCacheController : ControllerBase
{
    private readonly ILogger<HttpCacheController> _logger;
    private readonly RequestHandler _handler;
    private readonly HttpSettings _settings;

    private readonly HttpAccessSettings _accessSettings;

    private readonly HashSet<IPAddressRange> _blacklistedRanges;
    private readonly HashSet<IPAddressRange> _whitelistedRanges;

    private readonly HashSet<string> _blacklistedHosts;
    private readonly HashSet<string> _whitelistedHosts;

    private readonly HashSet<string> _filteredHeaders;

    public HttpCacheController(
        ILogger<HttpCacheController> logger,
        RequestHandler handler,
        HttpSettings settings
    )
    {
        _logger = logger;
        _handler = handler;
        _settings = settings;

        _accessSettings = _settings.Access;
        _blacklistedRanges = settings.GetBlacklistedRanges();
        _whitelistedRanges = settings.GetWhitelistedRanges();
        _blacklistedHosts = settings.GetBlacklistedHosts();
        _whitelistedHosts = settings.GetWhitelistedHosts();

        _filteredHeaders = _settings.FilteredHeaders;

        _logger.LogInformation("Initializing HTTP cache controller.");
    }

    public async Task HandleRequest(TimeSpan? maxAge = null)
    {
        var perfMon = new PerformanceMonitor();
        perfMon.Start(Constants.TotalTime);

        if (!Validate(Request))
            return;

        var message = await ConstructMessage(Request);

        var requestUri = message.RequestUri;
        var result = await _handler.HandleMessage(message, perfMon);

        await RespondWith(result.Response, Response);
        await Response.CompleteAsync();
        perfMon.Stop(Constants.TotalTime);

        _logger.LogInformation(
            "Handled {Cached} request in {Elapsed} ms, with {ElapsedDb} ms of DB delay " +
            "with destination: {Destination}",
            result.IsCached 
                ? "cached" 
                : "non-cached",
            perfMon.GetElapsed(Constants.TotalTime).TotalMilliseconds,
            perfMon.GetElapsed(Constants.DatabaseTime).TotalMilliseconds,
            requestUri?.ToString() ?? "<unknown>"
        );
    }


    private bool Validate(HttpRequest request)
    {
        var hostIps = GetActualHostIp(request, out var host);
        var sourceIp = GetSourceIp(request);

        return Validate(sourceIp ?? IPAddress.None, hostIps, host);
    }

    private bool Validate(IPAddress sourceIp, IPAddress[] hostIps, string host)
    {
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
    private async Task<HttpRequestMessage> ConstructMessage(HttpRequest request)
    {
        var method = request.Method;
        var headers = request.Headers;
        var hostHeader = headers[_settings.HostHeader];

        if (hostHeader.Count != 1 || hostHeader[0] == null)
            throw new Exception("Invalid actual host header specified.");

        var actualHost = new Uri(hostHeader[0]!);
        var actualMethod = new System.Net.Http.HttpMethod(method);

        byte[]? contentBytes = null;

        if (request.Body != null && request.Body.CanRead)
        {
            var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms);
            contentBytes = ms.ToArray();
        }
        
        var message = new HttpRequestMessage(actualMethod, actualHost)
        {
            Version = request.Protocol switch
            {
                "HTTP/1.0" => HttpVersion.Version10,
                "HTTP/1.1" => HttpVersion.Version11,
                "HTTP/2.0" => HttpVersion.Version20,
                _ => throw new ArgumentOutOfRangeException()
            },
            Content = contentBytes != null
                ? new ByteArrayContent(contentBytes)
                : null
        };

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

    private IEnumerable<KeyValuePair<string, string[]>> FilterHeaders(
        IEnumerable<KeyValuePair<string, string[]>> headers
    ) => headers.Where(header => !_settings.FilteredHeaders.Contains(header.Key));

    [NonAction]
    private async Task RespondWith(Response message, HttpResponse response)
    {
        response.StatusCode = (int)message.StatusCode;

        foreach (var header in FilterHeaders(message.Headers))
            response.Headers.Add(
                HttpUtility.UrlEncode(header.Key),
                new StringValues(header.Value
                    .Select(HttpUtility.UrlEncode)
                    .ToArray()
                )
            );

        if (message.ContentHeaders != null)
            foreach (var header in FilterHeaders(message.ContentHeaders))
                response.Headers.Add(
                    HttpUtility.UrlEncode(header.Key),
                    new StringValues(header.Value
                        .Select(HttpUtility.UrlEncode)
                        .ToArray()
                    )
                );


        await response.BodyWriter.WriteAsync(message.Content);
        await response.BodyWriter.FlushAsync();

        foreach (var header in FilterHeaders(message.TrailingHeaders))
            response.AppendTrailer(
                HttpUtility.UrlEncode(header.Key),
                new StringValues(header.Value
                    .Select(HttpUtility.UrlEncode)
                    .ToArray()
                )
            );
    }
}