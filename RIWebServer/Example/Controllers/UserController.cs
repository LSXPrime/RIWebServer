using System.Net;
using System.Text.Json;
using RIWebServer.Attributes.Content;
using RIWebServer.Attributes.Http;
using RIWebServer.Authentication.Middleware;
using RIWebServer.Example.Database;
using RIWebServer.Example.Entities;
using RIWebServer.Middleware;
using RIWebServer.Requests;

namespace RIWebServer.Example.Controllers;

public class UserController
{
    private readonly AppDbContext? _dbContext;

    public UserController(AppDbContext? dbContext)
    {
        _dbContext = dbContext;
    }

    public UserController()
    {
    }

    [RiGet("GetAll")]
    [Middleware(typeof(AuthenticationMiddleware))]
    public RiResponse GetUsers()
    {
        var users = _dbContext?.UsersData.GetAll() ?? [];
        return new RiResponse(JsonSerializer.Serialize(users))
        {
            ContentType = "application/json",
            StatusCode = HttpStatusCode.OK,
        };
    }

    [RiGet("GetUsersByGroupId/{GroupId}")]
    [Middleware(typeof(AuthenticationMiddleware))]
    public RiResponse GetUsersByGroupId(RiRequest request)
    {
        var groupIdStr = request.RouteParams["GroupId"];
        if (!int.TryParse(groupIdStr, out var groupId))
            return new RiResponse("Invalid Group ID.")
            {
                StatusCode = HttpStatusCode.BadRequest,
            };
        
        var users = _dbContext?.UsersData.GetAllWithRelated()
            .Where(b => b.UserGroupId == groupId).ToArray();
        
        if (users == null)
            return new RiResponse("Users not found.")
            {
                StatusCode = HttpStatusCode.NotFound,
            };
        return new RiResponse(JsonSerializer.Serialize(users))
        {
            ContentType = "application/json",
            StatusCode = HttpStatusCode.OK,
        };
    }


    [RiGet("GetUserById/{UserId}")]
    public RiResponse GetUserById(RiRequest request)
    {
        var userIdStr = request.RouteParams["UserId"];
        if (!int.TryParse(userIdStr, out var userId))
            return new RiResponse("Invalid User ID.")
            {
                StatusCode = HttpStatusCode.BadRequest,
            };

        var user = _dbContext?.UsersData.GetById(userId);
        return user != null
            ? new RiResponse(JsonSerializer.Serialize(user))
            {
                StatusCode = HttpStatusCode.OK,
                ContentType = "application/json"
            }
            : new RiResponse("User not found.")
            {
                StatusCode = HttpStatusCode.NotFound,
            };
    }

    [RiPost("CreateUser")]
    [Middleware(typeof(AuthenticationMiddleware))]
    public RiResponse CreateUser([FromBody] UserData? user)
    {
        if (user == null)
            return new RiResponse("Invalid User data.")
            {
                StatusCode = HttpStatusCode.BadRequest,
            };

        if (_dbContext?.Users.GetById(user.Id) != null)
            return new RiResponse($"User with ID {user.Id} already exists.")
            {
                StatusCode = HttpStatusCode.Conflict,
            };


        var groupExists = _dbContext?.UserGroups.GetById(user.UserGroupId) != null;
        if (!groupExists)
            return new RiResponse($"User group with ID {user.UserGroupId} not found.")
            {
                StatusCode = HttpStatusCode.NotFound,
            };

        _dbContext?.UsersData.Add(user);
        _dbContext?.SaveChanges();

        return new RiResponse($"User with ID {user.Id} created successfully.")
        {
            StatusCode = HttpStatusCode.Created,
        };
    }

    [RiPut("UpdateUser/{UserId}")]
    [Middleware(typeof(AuthenticationMiddleware))]
    public RiResponse UpdateUser(RiRequest request, [FromBody] UserData? user)
    {
        if (user == null || !int.TryParse(request.RouteParams["UserId"], out var userId) || userId != user.Id)
            return new RiResponse("Invalid User data or ID.")
            {
                StatusCode = HttpStatusCode.BadRequest,
            };

        if (_dbContext?.UsersData.GetById(user.Id) == null)
            return new RiResponse($"User with ID {user.Id} not found.")
            {
                StatusCode = HttpStatusCode.NotFound,
            };

        _dbContext?.UsersData.Update(user);
        _dbContext?.SaveChanges();

        return new RiResponse($"User with ID {user.Id} updated successfully.")
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    [RiDelete("DeleteUser/{UserId}")]
    [Middleware(typeof(AuthenticationMiddleware))]
    public RiResponse DeleteUser(RiRequest request)
    {
        var userIdStr = request.RouteParams["UserId"];
        if (!int.TryParse(userIdStr, out var userId))
            return new RiResponse("Invalid User ID.")
            {
                StatusCode = HttpStatusCode.BadRequest,
            };

        _dbContext?.UsersData.Delete(userId);
        _dbContext?.SaveChanges();

        return new RiResponse($"User with ID {userId} deleted successfully.")
        {
            StatusCode = HttpStatusCode.OK,
        };
    }
}