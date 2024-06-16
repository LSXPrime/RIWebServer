using RIWebServer.Requests;

namespace RIWebServer.Middleware;

public class MiddlewareManager
{
    private readonly List<IMiddleware> _globalMiddlewares = [];

    public void AddGlobalMiddleware(IMiddleware middleware)
    {
        _globalMiddlewares.Add(middleware);
    }

    /// <summary>
    /// Invokes the middleware pipeline asynchronously.
    /// </summary>
    /// <param name="request">The request object.</param>
    /// <param name="response">The response object.</param>
    /// <param name="middlewareAttributes">Optional collection of middleware attributes.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeMiddleware(RiRequest request, RiResponse response, IEnumerable<MiddlewareAttribute>? middlewareAttributes = null)
    {
        var pipeline = BuildMiddlewarePipeline(response, middlewareAttributes);
        await pipeline(request);
    }

    /// <summary>
    /// Builds the middleware pipeline.
    /// </summary>
    /// <param name="response">The response object.</param>
    /// <param name="middlewareAttributes">Optional collection of middleware attributes.</param>
    /// <returns>The middleware pipeline.</returns>
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

    /// <summary>
    /// Builds a middleware delegate that wraps the given middleware and the next delegate in a pipeline.
    /// </summary>
    /// <param name="middleware">The middleware to be wrapped.</param>
    /// <param name="nextDelegate">The next delegate in the pipeline, or null if there is no next delegate.</param>
    /// <param name="response">The response object.</param>
    /// <returns>A delegate that wraps the given middleware and the next delegate in a pipeline.</returns>
    private Func<RiRequest, Task> BuildMiddlewareDelegate(IMiddleware middleware,
        Func<RiRequest, Task>? nextDelegate, RiResponse response)
    {
        return req => middleware.InvokeAsync(req, response, () => nextDelegate?.Invoke(req) ?? Task.CompletedTask);
    }
}