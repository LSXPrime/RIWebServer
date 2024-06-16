namespace RIWebServer.Attributes.Http;

// Attribute for HTTP GET method
public class RiGetAttribute(string route = "") : RiRouteBase(route);