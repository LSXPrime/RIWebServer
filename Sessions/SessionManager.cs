// Sessions/SessionManager.cs
using System.Collections.Concurrent;
using System.Security.Cryptography;
using RIWebServer.Requests;

namespace RIWebServer.Sessions;

public class SessionManager
{
    private readonly ConcurrentDictionary<string, RiSession> _sessions = new();
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(20);
    private const string SessionCookieName = "SESSION_ID";

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

    private string GetSessionIdFromCookie(RiRequest request)
    {
        return request.Cookies.GetValueOrDefault(SessionCookieName)!;
    }

    private string GenerateSessionId()
    {
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        return Convert.ToBase64String(randomBytes);
    }

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