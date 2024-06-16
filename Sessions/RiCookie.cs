namespace RIWebServer.Sessions;

public class RiCookie(string name, string value)
{
    public string Name { get; set; } = name;
    public string Value { get; set; } = value;
    public DateTime? Expires { get; set; }
    public string Path { get; set; }
    public bool HttpOnly { get; set; }
    public bool Secure { get; set; }

    /// <summary>
    /// Returns a string representation of the cookie in the format: "Name=Value;[Expires=RFC1123 formatted date];[Path=path];[HttpOnly];[Secure]".
    /// </summary>
    /// <returns>The string representation of the cookie.</returns>
    public override string ToString()
    {
        var cookieString = $"{Name}={Value};";
        if (Expires.HasValue)
            cookieString += $" Expires={Expires.Value.ToUniversalTime():R};"; // RFC1123 format
        if (!string.IsNullOrEmpty(Path))
            cookieString += $" Path={Path};";
        if (HttpOnly)
            cookieString += " HttpOnly;";
        if (Secure)
            cookieString += " Secure;"; 
        return cookieString;
    }
}