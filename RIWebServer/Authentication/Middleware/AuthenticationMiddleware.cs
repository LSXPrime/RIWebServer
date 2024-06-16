using System.Net;
using System.Text;
using System.Text.Json;
using RIWebServer.Middleware;
using RIWebServer.Requests;

namespace RIWebServer.Authentication.Middleware;

public class AuthenticationMiddleware : IMiddleware
{
    public async Task InvokeAsync(RiRequest request, RiResponse response, Func<Task> next)
    {
        if (request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var tokenParts = authHeader.Split(' ');
            if (tokenParts is ["Bearer", _])
            {
                var token = tokenParts[1];
                var userId = ValidateToken(token);
                if (userId != null)
                {
                    request.User = AuthenticationManager.GetUserById(userId.Value);

                    await next();
                    return;
                }
            }
        }

        
        response.StatusCode = HttpStatusCode.Unauthorized;
        response.ContentLength = Encoding.UTF8.GetByteCount(response.Body);
    }

    private int? ValidateToken(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return null;
            }

            var encodedHeader = parts[0];
            var encodedPayload = parts[1];
            var encodedSignature = parts[2];

            var header = JsonSerializer.Deserialize<Dictionary<string, object>>(AuthenticationManager.Base64UrlDecode(encodedHeader));
            var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(AuthenticationManager.Base64UrlDecode(encodedPayload));

            var expectedSignature = AuthenticationManager.Base64UrlEncode(
                Convert.ToBase64String(AuthenticationManager.GenerateSignature(encodedHeader, encodedPayload)));

            if (expectedSignature != encodedSignature)
            {
                return null;
            }

            if (payload!.TryGetValue("exp", out var expObj) && expObj is long exp)
            {
                var expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp);
                if (expirationTime < DateTimeOffset.UtcNow)
                {
                    return null;
                }
            }

            if (payload.TryGetValue("sub", out var subObj) && int.TryParse(subObj.ToString(), out var userId))
            {
                return userId;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error validating token: {ex.Message}");
            return null;
        }
    }
}