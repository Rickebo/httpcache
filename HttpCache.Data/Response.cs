using System.Net;
using System.Text.Json.Serialization;
using HttpCache.Data;

namespace HttpCache.Data;

public class Response : HttpMessage
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HttpStatusCode StatusCode { get; set; }
}