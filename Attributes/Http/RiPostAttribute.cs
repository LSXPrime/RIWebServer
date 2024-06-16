namespace RIWebServer.Attributes.Http;

// Attribute for HTTP POST method
public class RiPostAttribute(string route = "") : RiRouteBase(route);