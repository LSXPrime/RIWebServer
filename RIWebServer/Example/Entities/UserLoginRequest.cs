namespace RIWebServer.Example.Entities;

public class UserLoginRequest
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
}