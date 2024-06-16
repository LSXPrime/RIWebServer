using RIWebServer.Requests;

namespace RIWebServer.Middleware;

public class MiddlewareManager
{
    private readonly List<IMiddleware> _globalMiddlewares = [];

    public void AddGlobalMiddleware(IMiddleware middleware)
    {
        _globalMiddlewares.Add(middleware);
    }

    public async Task InvokeMiddleware(RiRequest request, RiResponse response, IEnumerable<MiddlewareAttribute>? middlewareAttributes = null)
    {
        var pipeline = BuildMiddlewarePipeline(response, middlewareAttributes);
        await pipeline(request);
    }

    private Func<RiRequest, Task> BuildMiddlewarePipeline(RiResponse response, IEnumerable<MiddlewareAttribute>? middlewareAttributes = null)
    {
        Func<RiRequest, Task> currentDelegate = null!;

        for (var i = _globalMiddlewares.Count - 1; i >= 0; i--)
            currentDelegate = BuildMiddlewareDelegate(_globalMiddlewares[i], currentDelegate, response); 

        if (middlewareAttributes != null)
        {
            currentDelegate = middlewareAttributes.Reverse().Select(attribute => (IMiddleware)Activator.CreateInstance(attribute.MiddlewareType)!).Aggregate(currentDelegate, (current, middleware) => BuildMiddlewareDelegate(middleware, current, response));
        }

        return currentDelegate ?? (_ => Task.CompletedTask);
    }

    private Func<RiRequest, Task> BuildMiddlewareDelegate(IMiddleware middleware,
        Func<RiRequest, Task>? nextDelegate, RiResponse response)
    {
        return req => middleware.InvokeAsync(req, response, () => nextDelegate?.Invoke(req) ?? Task.CompletedTask);
    }
}