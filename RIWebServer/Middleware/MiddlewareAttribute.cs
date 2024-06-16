namespace RIWebServer.Middleware;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class MiddlewareAttribute(Type middlewareType) : Attribute
{
    public Type MiddlewareType { get; } = middlewareType;
}