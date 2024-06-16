namespace RIWebServer.Attributes.Http;

// Attribute for HTTP PUT method
public class RiPutAttribute(string route = "") : RiRouteBase(route);