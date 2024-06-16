using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RIWebServer.Authentication.Database;
using RIWebServer.Authentication.Entities;

namespace RIWebServer.Authentication;

public class AuthenticationManager
{
    private static string? _tokenSecret;
    private static AuthenticationDbContext? _dbContext;

    public AuthenticationManager(AuthenticationDbContext? dbContext, string? tokenSecret)
    {
        _dbContext = dbContext;
        _tokenSecret = tokenSecret;
    }
    
    public static User? GetUserById(int id)
    {
        return _dbContext?.Users.GetById(id);
    }

    public User? RegisterUser(string username, string password, string email = "", string role = "user")
    {
        var passwordHash = HashPassword(password);

        var newUser = new User
        {
            Username = username,
            PasswordHash = passwordHash,
            Email = email,
            Role = role
        };

        _dbContext?.Users.Add(newUser);
        var result = _dbContext.SaveChanges();

        return result ? newUser : null;
    }

    public string? LoginUser(string username, string password)
    {
        var user = _dbContext?.Users.GetAll().FirstOrDefault(u => u.Username == username);
        if (user == null)
        {
            return null;
        }

        return !VerifyPassword(password, user.PasswordHash) ? null : GenerateToken(user);
    }


    private string HashPassword(string password)
    {
        var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private bool VerifyPassword(string password, string passwordHash)
    {
        var hashedPassword = HashPassword(password);
        return string.Equals(hashedPassword, passwordHash);
    }

    private string GenerateToken(User user)
    {
        var header = new Dictionary<string, object>()
        {
            { "alg", "HS256" },
            { "typ", "JWT" }
        };

        var payload = new Dictionary<string, object>()
        {
            { "sub", user.Id },
            { "name", user.Username },
            { "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
            { "exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }
        };

        var encodedHeader = Base64UrlEncode(JsonSerialize(header));
        var encodedPayload = Base64UrlEncode(JsonSerialize(payload));

        var signature = GenerateSignature(encodedHeader, encodedPayload);
        var encodedSignature = Base64UrlEncode(Convert.ToBase64String(signature)); 
        
        return $"{encodedHeader}.{encodedPayload}.{encodedSignature}";
    }
    
    public static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
    
    public static string Base64UrlDecode(string input)
    {
        var output = input
            .Replace('-', '+')
            .Replace('_', '/');
        switch (output.Length % 4) // Pad with trailing '='s
        {
            case 0: break;
            case 2:
                output += "==";
                break;
            case 3:
                output += "=";
                break;
            default: throw new Exception("Illegal base64url string!");
        }

        var converted = Convert.FromBase64String(output);
        return Encoding.UTF8.GetString(converted);
    }

    private static string JsonSerialize(object obj)
    {
        return JsonSerializer.Serialize(obj);
    }

    public static byte[] GenerateSignature(string encodedHeader, string encodedPayload)
    {
        
        var keyBytes = Encoding.UTF8.GetBytes(_tokenSecret!);
        var inputBytes = Encoding.UTF8.GetBytes($"{encodedHeader}.{encodedPayload}");

        using var hmac = new HMACSHA256(keyBytes);
        return hmac.ComputeHash(inputBytes);
    }
}