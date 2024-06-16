using System.Collections.Concurrent;
using System.Security.Cryptography;
using RIWebServer.Requests;

namespace RIWebServer.Sessions;

public class SessionManager
{
    private readonly ConcurrentDictionary<string, RiSession> _sessions = new();
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(20);
    private const string SessionCookieName = "SESSION_ID";

    /// <summary>
    /// Asynchronously retrieves or creates a session for the given request.
    /// </summary>
    /// <param name="request">The request to retrieve or create a session for.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the retrieved or created session.</returns>
    public Task<RiSession> GetOrCreateSessionAsync(RiRequest request)
    {
        var sessionId = GetSessionIdFromCookie(request);
        if (!string.IsNullOrEmpty(sessionId) && _sessions.TryGetValue(sessionId, out var session))
        {
            session.LastAccessed = DateTime.Now;
            return Task.FromResult(session);
        }

        sessionId = GenerateSessionId();
        session = new RiSession(sessionId) { LastAccessed = DateTime.Now };
        _sessions[sessionId] = session;

        // Create and set the session cookie
        request.Cookies[SessionCookieName] = sessionId;

        return Task.FromResult(session);
    }

    /// <summary>
    /// Retrieves the session ID from the cookie of the given request.
    /// </summary>
    /// <param name="request">The request to retrieve the session ID from.</param>
    /// <returns>The session ID stored in the cookie, or null if the cookie does not exist or has no value.</returns>
    private string GetSessionIdFromCookie(RiRequest request)
    {
        return request.Cookies.GetValueOrDefault(SessionCookieName)!;
    }

    /// <summary>
    /// Generates a random session ID using cryptographically secure random bytes.
    /// </summary>
    /// <returns>A base64-encoded string representing the generated session ID.</returns>
    private string GenerateSessionId()
    {
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Asynchronously starts the cleanup process for expired sessions.
    /// The cleanup process runs indefinitely, checking for expired sessions every 5 minutes.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartCleanup()
    {
        while (true)
        {
            var expiredSessions = _sessions.Where(s => DateTime.Now - s.Value.LastAccessed > _sessionTimeout).ToList();
            foreach (var (sessionId, _) in expiredSessions)
            {
                _sessions.TryRemove(sessionId, out _);
            }

            await Task.Delay(TimeSpan.FromMinutes(5)); 
        }
    }
}