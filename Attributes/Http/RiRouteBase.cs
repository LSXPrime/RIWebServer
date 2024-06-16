namespace RIWebServer.Attributes.Http;

[AttributeUsage(AttributeTargets.Method)]
public class RiRouteBase(string route = "") : Attribute
{
    public string Route { get; } = route;
}