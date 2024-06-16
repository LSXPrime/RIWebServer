using RIWebServer.Attributes.ORM;

namespace RIWebServer.Authentication.Entities;

public class User
{
    [PrimaryKey] 
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string Role { get; set; } = null!;
}