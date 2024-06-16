using RIWebServer.Middleware;
using RIWebServer.Requests;

namespace RIWebServer.Example.Middleware;

public class LoggingMiddleware : IMiddleware
{
    /// <summary>
    /// Invokes the logging middleware asynchronously.
    /// Writes request and response information to the console.
    /// </summary>
    /// <param name="request">The RiRequest object representing the incoming request.</param>
    /// <param name="response">The RiResponse object representing the response.</param>
    /// <param name="next">A delegate representing the next middleware in the chain.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(RiRequest request, RiResponse response, Func<Task> next)
    {
        Console.WriteLine(
            $"[LoggingMiddleware] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Request: {request.Method} {request.Path} ({response.StatusCode})");

        Console.WriteLine($"[LoggingMiddleware] Headers:");
        foreach (var header in request.Headers)
            Console.WriteLine($"[LoggingMiddleware]  - {header.Key}: {header.Value}");

        await next();

        Console.WriteLine(
            $"[LoggingMiddleware] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Response: {response.StatusCode} {response.Body}\n\n");
    }
}