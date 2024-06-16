using RIWebServer.Requests;

namespace RIWebServer.Middleware;

public interface IMiddleware
{
    Task InvokeAsync(RiRequest request, RiResponse response, Func<Task> next);
}