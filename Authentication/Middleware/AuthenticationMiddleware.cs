using System.Net;
using System.Text;
using System.Text.Json;
using RIWebServer.Middleware;
using RIWebServer.Requests;

namespace RIWebServer.Authentication.Middleware;

public class AuthenticationMiddleware : IMiddleware
{
    /// <summary>
    /// Asynchronously invokes the middleware for the given request and response.
    /// If the request contains an "Authorization" header with a valid token,
    /// the user ID is extracted from the token and set as the request's user.
    /// If the token is valid, the next middleware in the pipeline is invoked.
    /// If the token is invalid or missing, the response is set to Unauthorized status.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="response">The outgoing response.</param>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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

    /// <summary>
    /// Validates a token by splitting it into its header, payload, and signature parts.
    /// Decodes the header and payload using Base64Url encoding and deserializes them into dictionaries.
    /// Generates the expected signature by signing the header and payload and encodes it using Base64Url.
    /// Checks if the expected signature matches the provided signature.
    /// If the payload contains an "exp" field, checks if it is still valid (not expired).
    /// If the payload contains a "sub" field, tries to parse it as an integer and returns it.
    /// Returns null if any of the validation steps fail.
    /// </summary>
    /// <param name="token">The token to be validated.</param>
    /// <returns>The user ID parsed from the payload if the token is valid, null otherwise.</returns>
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