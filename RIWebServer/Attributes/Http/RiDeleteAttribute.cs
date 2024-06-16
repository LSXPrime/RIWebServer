namespace RIWebServer.Attributes.Http;

// Attribute for HTTP DELETE method
public class RiDeleteAttribute(string route = "") : RiRouteBase(route);