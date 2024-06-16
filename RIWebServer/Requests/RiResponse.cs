using System.Net;
using System.Text;
using RIWebServer.Sessions;

namespace RIWebServer.Requests;

public class RiResponse(string body = "")
{
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    public string ContentType { get; set; } = "text/html";
    public long ContentLength { get; set; } = Encoding.UTF8.GetByteCount(body);
    public string Body { get; set; } = body;
    public List<RiCookie> Cookies { get; set; } = [];
}