using System.Net;
using RIWebServer.Example.Controllers;
using RIWebServer.Example.Database;
using RIWebServer.Example.Middleware;
using RIWebServer.Requests;
using AuthenticationManager = RIWebServer.Authentication.AuthenticationManager;

namespace RIWebServer;

internal static class Program
{
    private static async Task Main()
    {
        var server = new RiWebServer(8080, ipAddress: "127.0.0.1");
        const string connectionString = "Data Source=mydb.sqlite;";
        var dbContext = new AppDbContext(connectionString);
        dbContext.EnsureDatabaseCreated();

        var authManager = new AuthenticationManager(dbContext, "secret");

        // Add global middleware
        server.AddMiddleware(new LoggingMiddleware());

        // Define routes and their handlers
        server.AddRoute("/", _ => new RiResponse("<h1>Welcome to the Advanced Server!</h1>")
        {
            StatusCode = HttpStatusCode.OK,
            ContentType = "text/html"
        });
        server.AddRoute("/about", "<h2>About Us</h2><p>This is a simple web server built with C#.</p>");
        var fileExists = File.Exists(@"C:\External\Prompts\Phi-3.json");
        server.AddRoute("/myfile.txt",
            new RiResponse(fileExists
                ? await File.ReadAllTextAsync(@"C:\External\Prompts\Phi-3.json")
                : "File not found")
            {
                StatusCode = fileExists ? HttpStatusCode.OK : HttpStatusCode.NotFound,
                ContentType = fileExists ? "application/json" : "text/html",
            }
        );

        // Register controllers
        server.MapController(() => new HomeController());
        server.MapController(() => new UserController(dbContext), "/users");
        server.MapController(() => new AuthenticationController(authManager), "/auth");


        await server.Start();
    }
}