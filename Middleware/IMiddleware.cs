using RIWebServer.Requests;

namespace RIWebServer.Middleware;

public interface IMiddleware
{
    /// <summary>
    /// Asynchronously invokes the middleware for the given request and response.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="response">The outgoing response.</param>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvokeAsync(RiRequest request, RiResponse response, Func<Task> next);
}