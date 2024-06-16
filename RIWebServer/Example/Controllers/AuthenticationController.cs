using System.Net;
using RIWebServer.Attributes.Content;
using RIWebServer.Attributes.Http;
using RIWebServer.Example.Entities;
using RIWebServer.Requests;
using AuthenticationManager = RIWebServer.Authentication.AuthenticationManager;

namespace RIWebServer.Example.Controllers;

public class AuthenticationController
{
    private readonly AuthenticationManager? _authenticationService;

    public AuthenticationController() { }

    public AuthenticationController(AuthenticationManager authenticationService)
    {
        _authenticationService = authenticationService;
    }

    [RiPost("register")]
    public RiResponse RegisterUser([FromBody] UserRegistrationRequest request)
    {
        var user = _authenticationService?.RegisterUser(request.Username, request.Password, request.Email);
        return user != null
            ? new RiResponse("User registered successfully.")
            {
                StatusCode = HttpStatusCode.Created,
            }
            : new RiResponse("Failed to register user.")
            {
                StatusCode = HttpStatusCode.BadRequest,
            };
    }

    [RiPost("login")]
    public RiResponse LoginUser([FromBody] UserLoginRequest request)
    {
        var token = _authenticationService?.LoginUser(request.Username, request.Password);
        return token != null
            ? new RiResponse(token)
            {
                StatusCode = HttpStatusCode.OK,
            }
            : new RiResponse("Invalid username or password.")
            {
                StatusCode = HttpStatusCode.Unauthorized,
            };
    }
}