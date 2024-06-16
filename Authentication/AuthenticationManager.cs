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
    
    /// <summary>
    /// Retrieves a user from the database based on their ID.
    /// </summary>
    /// <param name="id">The ID of the user to retrieve.</param>
    /// <returns>The user with the specified ID, or null if no user is found.</returns>
    public static User? GetUserById(int id)
    {
        return _dbContext?.Users.GetById(id);
    }

    /// <summary>
    /// Registers a new user in the system with the provided username, password, email, and role.
    /// </summary>
    /// <param name="username">The username of the new user.</param>
    /// <param name="password">The password of the new user.</param>
    /// <param name="email">The email of the new user. Defaults to an empty string if not provided.</param>
    /// <param name="role">The role of the new user. Defaults to "user" if not provided.</param>
    /// <returns>The newly registered user if the save operation was successful, otherwise null.</returns>
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
        var result = _dbContext!.SaveChanges();

        return result ? newUser : null;
    }

    /// <summary>
    /// Authenticates a user with the provided username and password.
    /// </summary>
    /// <param name="username">The username of the user to authenticate.</param>
    /// <param name="password">The password of the user to authenticate.</param>
    /// <returns>The JWT token if the authentication was successful, otherwise null.</returns>
    public string? LoginUser(string username, string password)
    {
        var user = _dbContext?.Users.GetAll().FirstOrDefault(u => u.Username == username);
        if (user == null)
        {
            return null;
        }

        return !VerifyPassword(password, user.PasswordHash) ? null : GenerateToken(user);
    }
    
    /// <summary>
    /// Hashes a password using SHA256 algorithm and returns the hashed password as a base64 string.
    /// </summary>
    /// <param name="password">The password to be hashed.</param>
    /// <returns>The hashed password as a base64 string.</returns>
    private string HashPassword(string password)
    {
        var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    /// <summary>
    /// Verifies if the provided password matches the hashed password.
    /// </summary>
    /// <param name="password">The password to verify.</param>
    /// <param name="passwordHash">The hashed password to compare against.</param>
    /// <returns>True if the passwords match, false otherwise.</returns>
    private bool VerifyPassword(string password, string passwordHash)
    {
        var hashedPassword = HashPassword(password);
        return string.Equals(hashedPassword, passwordHash);
    }

    /// <summary>
    /// Generates a JWT token for the given user.
    /// </summary>
    /// <param name="user">The user for whom the token is being generated.</param>
    /// <returns>The generated JWT token.</returns>
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
    
    /// <summary>
    /// Encodes a string to Base64 URL format.
    /// </summary>
    /// <param name="input">The input string to encode.</param>
    /// <returns>The Base64 URL encoded string.</returns>
    public static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
    
    /// <summary>
    /// Decodes a Base64 URL encoded string.
    /// </summary>
    /// <param name="input">The Base64 URL encoded string to decode.</param>
    /// <returns>The decoded string.</returns>
    public static string Base64UrlDecode(string input)
    {
        var output = input
            .Replace('-', '+')
            .Replace('_', '/');
        switch (output.Length % 4)
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

    /// <summary>
    /// Generates a signature using HMACSHA256 algorithm based on the provided encoded header and payload.
    /// </summary>
    /// <param name="encodedHeader">The encoded header.</param>
    /// <param name="encodedPayload">The encoded payload.</param>
    /// <returns>The generated signature as a byte array.</returns>
    public static byte[] GenerateSignature(string encodedHeader, string encodedPayload)
    {
        
        var keyBytes = Encoding.UTF8.GetBytes(_tokenSecret!);
        var inputBytes = Encoding.UTF8.GetBytes($"{encodedHeader}.{encodedPayload}");

        using var hmac = new HMACSHA256(keyBytes);
        return hmac.ComputeHash(inputBytes);
    }
}