namespace RIWebServer.Sessions;

public class RiCookie(string name, string value)
{
    public string Name { get; set; } = name;
    public string Value { get; set; } = value;
    public DateTime? Expires { get; set; }
    public string Path { get; set; }
    public bool HttpOnly { get; set; }
    public bool Secure { get; set; }

    public override string ToString()
    {
        var cookieString = $"{Name}={Value};";
        if (Expires.HasValue)
            cookieString += $" Expires={Expires.Value.ToUniversalTime().ToString("R")};"; // RFC1123 format
        if (!string.IsNullOrEmpty(Path))
            cookieString += $" Path={Path};";
        if (HttpOnly)
            cookieString += " HttpOnly;";
        if (Secure)
            cookieString += " Secure;"; 
        return cookieString;
    }
}