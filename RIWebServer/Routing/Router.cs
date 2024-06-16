using System.Text.RegularExpressions;
using RIWebServer.Middleware;
using RIWebServer.Requests;

namespace RIWebServer.Routing;

public class Router
{
    private readonly List<Route> _routes = [];
    public void AddRoute(string pathTemplate, Func<RiRequest, RiResponse> handler, string httpMethod = "GET", IEnumerable<MiddlewareAttribute>? middlewareAttributes = null) 
        => AddRoute(pathTemplate, (req) => Task.FromResult(handler(req)), httpMethod, middlewareAttributes); 
    public void AddRoute(string pathTemplate, Func<RiRequest, Task<RiResponse>> handler, string httpMethod = "GET", IEnumerable<MiddlewareAttribute>? middlewareAttributes = null)
    {
        // Escape special regex characters except for {param} placeholders
        var regexPattern = "^" + Regex.Replace(pathTemplate, @"{([^}]+)}", @"(?<$1>[^/]+)") + "$";
        // Replace {param} with named capture groups
        

        _routes.Add(new Route
        {
            Regex = new Regex(regexPattern),
            Handler = handler,
            HttpMethod = httpMethod,
            MiddlewareAttributes = middlewareAttributes?.ToList() ?? []
        });
    }

    public RouteResult? Route(RiRequest request)
    {
        foreach (var route in _routes)
        {
            var match = route.Regex.Match(request.Path);
            
            // Check if route matches
            if (!match.Success ||
                !string.Equals(request.Method, route.HttpMethod, StringComparison.OrdinalIgnoreCase)) continue;
            // Route found, extract route parameters
            request.RouteParams = match.Groups
                .Where<Group>(g => g.Success && !string.IsNullOrEmpty(g.Value) && g.Name != "0")
                .ToDictionary<Group, string, string>(g => g.Name, g => g.Value);

            return new RouteResult
            {
                Handler = route.Handler,
                RouteParams = request.RouteParams,
                MiddlewareAttributes = route.MiddlewareAttributes
            };
        }

        return null;
    }
}

public class Route
{
    public Regex Regex { get; set; } = null!;
    public Func<RiRequest, Task<RiResponse>> Handler { get; set; } = null!; // Change to Task<RiResponse>
    public string HttpMethod { get; set; } = null!; // Store the HTTP method
    public List<MiddlewareAttribute> MiddlewareAttributes { get; set; } = [];
}

public class RouteResult
{
    public Func<RiRequest, Task<RiResponse>> Handler { get; set; } = null!;
    public Dictionary<string, string> RouteParams { get; set; } = new(); 
    public List<MiddlewareAttribute> MiddlewareAttributes { get; set; } = [];
}