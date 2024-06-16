namespace RIWebServer.Example.Entities;

public class UserRegistrationRequest
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string Email { get; set; } = null!;
}