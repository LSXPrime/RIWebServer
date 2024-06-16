using RIWebServer.Middleware;
using RIWebServer.Requests;

namespace RIWebServer.Routing;

public class RouteResult
{
    public Func<RiRequest, Task<RiResponse>> Handler { get; set; } = null!;
    public Dictionary<string, string> RouteParams { get; set; } = new();
    public List<MiddlewareAttribute> MiddlewareAttributes { get; set; } = [];
}