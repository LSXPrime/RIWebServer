namespace RIWebServer.Sessions;

public class RiSession(string sessionId)
{
    public string SessionId { get; } = sessionId;
    public DateTime LastAccessed { get; set; }
    public Dictionary<string, object> Data { get; } = new();
}