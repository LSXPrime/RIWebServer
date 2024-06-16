using RIWebServer.Authentication.Entities;
using RIWebServer.Sessions;

namespace RIWebServer.Requests;

public class RiRequest
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public Dictionary<string, string> RouteParams { get; set; } = new();
    public string ProtocolVersion { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; } = new();
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, string> Cookies { get; } = new(); 
    public RiSession? Session { get; set; }
    public User? User { get; set; }
}