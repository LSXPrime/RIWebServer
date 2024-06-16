using RIWebServer.Middleware;
using RIWebServer.Requests;

namespace RIWebServer.Example.Middleware;

public class LoggingMiddleware : IMiddleware
{
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