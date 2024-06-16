using System.Text.RegularExpressions;
using RIWebServer.Middleware;
using RIWebServer.Requests;

namespace RIWebServer.Routing;

public class Route
{
    public Regex Regex { get; set; } = null!;
    public Func<RiRequest, Task<RiResponse>> Handler { get; set; } = null!;
    public string HttpMethod { get; set; } = null!;
    public List<MiddlewareAttribute> MiddlewareAttributes { get; set; } = [];
}