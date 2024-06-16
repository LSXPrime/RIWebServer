using System.Net;
using RIWebServer.Attributes.Http;
using RIWebServer.Requests;
using RIWebServer.Sessions;

namespace RIWebServer.Example.Controllers;

public class HomeController
{
    [RiGet]
    public string Index()
    {
        return "<h1>Welcome to the Home Controller!</h1>";
    }

    [RiGet]
    public string About()
    {
        return "<h2>About Us</h2><p>This is a page from the Home Controller.</p>";
    }
    
    [RiGet]
    public string Format()
    {
        var fileExists = File.Exists(@"C:\External\Prompts\Phi-3.json");
        return fileExists ? File.ReadAllText(@"C:\External\Prompts\Phi-3.json") : "File not found";
    }
    
    [RiGet]
    public RiResponse Prompt()
    {
        var fileExists = File.Exists(@"C:\External\Prompts\Phi-3.json");
        return new RiResponse
        {
            StatusCode = fileExists ? HttpStatusCode.OK : HttpStatusCode.NotFound,
            ContentType = fileExists ? "application/json" : "text/html",
            ContentLength = fileExists ? new FileInfo(@"C:\External\Prompts\Phi-3.json").Length : 0,
            Body = fileExists ? File.ReadAllText(@"C:\External\Prompts\Phi-3.json") : "File not found",
            Cookies =
            [
                new RiCookie("test", "3"),
                new RiCookie("Exists", fileExists.ToString())
            ]
        };
    }
}