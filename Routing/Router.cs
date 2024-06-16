using System.Text.RegularExpressions;
using RIWebServer.Middleware;
using RIWebServer.Requests;

namespace RIWebServer.Routing;

public class Router
{
    private readonly List<Route> _routes = [];

    /// <summary>
    /// Adds a route to the router with the given path template, handler, HTTP method, and optional middleware attributes.
    /// </summary>
    /// <param name="pathTemplate">The path template of the route.</param>
    /// <param name="handler">The handler function for the route.</param>
    /// <param name="httpMethod">The HTTP method of the route. Defaults to "GET".</param>
    /// <param name="middlewareAttributes">Optional middleware attributes for the route.</param>
    public void AddRoute(string pathTemplate, Func<RiRequest, RiResponse> handler, string httpMethod = "GET",
        IEnumerable<MiddlewareAttribute>? middlewareAttributes = null)
        => AddRoute(pathTemplate, (req) => Task.FromResult(handler(req)), httpMethod, middlewareAttributes);

    /// <summary>
    /// Adds a route to the router with the given path template, handler, HTTP method, and optional middleware attributes.
    /// </summary>
    /// <param name="pathTemplate">The path template of the route.</param>
    /// <param name="handler">The handler function for the route.</param>
    /// <param name="httpMethod">The HTTP method of the route. Defaults to "GET".</param>
    /// <param name="middlewareAttributes">Optional middleware attributes for the route.</param>        
    public void AddRoute(string pathTemplate, Func<RiRequest, Task<RiResponse>> handler, string httpMethod = "GET",
        IEnumerable<MiddlewareAttribute>? middlewareAttributes = null)
    {
        var regexPattern = "^" + Regex.Replace(pathTemplate, @"{([^}]+)}", @"(?<$1>[^/]+)") + "$";
        _routes.Add(new Route
        {
            Regex = new Regex(regexPattern),
            Handler = handler,
            HttpMethod = httpMethod,
            MiddlewareAttributes = middlewareAttributes?.ToList() ?? []
        });
    }

    /// <summary>
    /// Routes the given request to the appropriate handler based on the request's path and HTTP method.
    /// </summary>
    /// <param name="request">The request to route.</param>
    /// <returns>A RouteResult object containing the handler, route parameters, and middleware attributes, or null if no matching route is found.</returns>
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