using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;
using RIWebServer.Attributes.Content;
using RIWebServer.Middleware;
using RIWebServer.Requests;
using RIWebServer.Routing;
using RIWebServer.Sessions;

namespace RIWebServer;

public class RiWebServer(int port, string ipAddress = "")
{
    private readonly TcpListener _listener = new(string.IsNullOrEmpty(ipAddress) ? IPAddress.Any : IPAddress.Parse(ipAddress), port);

    private readonly SessionManager _sessionManager = new();
    private readonly Router _router = new();
    private readonly MiddlewareManager _middlewareManager = new();

    private readonly Dictionary<string, Func<object, string>> _supportedMediaTypes = new()
    {
        { "text/html", o => o.ToString()! },
        { "application/json", o => JsonSerializer.Serialize(o) },
        {
            "application/xml", o =>
            {
                using var stringWriter = new StringWriter();
                new XmlSerializer(o.GetType()).Serialize(stringWriter, o);
                return stringWriter.ToString();
            }
        },
        { "text/plain", o => o.ToString()! },
    };

    /// <summary>
    /// Asynchronously starts the server, listens for incoming TCP connections, and handles them.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task Start()
    {
        _listener.Start();
        Console.WriteLine("Server started on port " + _listener.LocalEndpoint);

        _ = _sessionManager.StartCleanup();
        while (true)
        {
            var client = await _listener.AcceptTcpClientAsync();
            Console.WriteLine($"Client connected from {client.Client.RemoteEndPoint}");
            _ = HandleClient(client);
        }
        // ReSharper disable once FunctionNeverReturns
    }

    /// <summary>
    /// Asynchronously handles a client connection by reading the request, parsing headers, reading the body if present, 
    /// managing sessions, routing the request, invoking middleware, and sending the HTTP response.
    /// </summary>
    /// <param name="client">The TCP client representing the connected client.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task HandleClient(TcpClient client)
    {
        var stream = client.GetStream();
        // Read the entire request
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var request = new RiRequest();
        try
        {
            while (await reader.ReadLineAsync() is { } line && !string.IsNullOrEmpty(line))
            {
                ParseHeader(request, line);
            }

            // Read body if any
            if (request.Headers.TryGetValue("Content-Length", out var value))
            {
                var contentLength = int.Parse(value);
                var buffer = new char[contentLength];
                await reader.ReadAsync(buffer, 0, contentLength);
                request.Body = new string(buffer);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading request: {ex.Message}");
            SendResponse(stream,
                new RiResponse { StatusCode = HttpStatusCode.InternalServerError, Body = "Internal Server Error" });
            return;
        }

        // --- Session Handling ---
        request.Session = await _sessionManager.GetOrCreateSessionAsync(request);

        // --- Routing ---
        var routeResult = _router.Route(request);
        var response = routeResult != null ? await routeResult.Handler(request) : new RiResponse("Not Found") { StatusCode = HttpStatusCode.NotFound };
        
        // --- Middleware Handling ---
        await _middlewareManager.InvokeMiddleware(request, response, routeResult?.MiddlewareAttributes);
        
        // Send the HTTP response
        SendResponse(stream, response);
        
        Console.WriteLine($"Client disconnected from: {client.Client.RemoteEndPoint}");
        client.Close();
    }

    /// <summary>
    /// Parses the header line of an HTTP request and updates the corresponding properties of the <see cref="RiRequest"/> object.
    /// </summary>
    /// <param name="request">The <see cref="RiRequest"/> object to update.</param>
    /// <param name="headerLine">The header line to parse.</param>
    private void ParseHeader(RiRequest request, string headerLine)
    {
        if (headerLine.StartsWith("GET", StringComparison.OrdinalIgnoreCase) ||
            headerLine.StartsWith("POST", StringComparison.OrdinalIgnoreCase) ||
            headerLine.StartsWith("PUT", StringComparison.OrdinalIgnoreCase) ||
            headerLine.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            var parts = headerLine.Split(' ');
            request.Method = parts[0];
            request.Path = parts[1];
            request.ProtocolVersion = parts[2];
        }
        else
        {
            var parts = headerLine.Split(':', 2);
            if (parts.Length == 2)
            {
                request.Headers[parts[0].Trim()] = parts[1].Trim();
            }
        }
    }

    /// <summary>
    /// Sends an HTTP response over the network stream, including the response header and body.
    /// </summary>
    /// <param name="stream">The network stream to send the response over.</param>
    /// <param name="response">The <see cref="RiResponse"/> object containing the response details.</param>
    private void SendResponse(NetworkStream stream, RiResponse response)
    {
        // Build the response header
        var responseHeader = $"HTTP/1.1 {(int)response.StatusCode} {response.StatusCode}\r\n" +
                             $"Content-Type: {response.ContentType}\r\n" +
                             $"Content-Length: {response.ContentLength}\r\n";

        // Add cookies to headers
        responseHeader =
            response.Cookies.Aggregate(responseHeader, (current, cookie) => current + $"Set-Cookie: {cookie}\r\n");

        responseHeader += "\r\n";

        var headerBytes = Encoding.UTF8.GetBytes(responseHeader);
        var bodyBytes = Encoding.UTF8.GetBytes(response.Body);

        stream.Write(headerBytes);
        stream.Write(bodyBytes);
    }
    
    /// <summary>
    /// Determines the content type of a file based on its file extension.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns>The content type of the file.</returns>
    private string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".txt" => "text/plain",
            ".css" => "text/css",
            ".js" => "text/javascript",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".xml" => "application/xml",
            ".json" => "application/json",
            ".html" or ".htm" => "text/html",
            ".zip" or ".rar" or ".exe" => "application/octet-stream",
            _ => "text/html"
        };
    }

    /// <summary>
    /// Negotiates the content type to use for the response based on the client's "Accept" header and supported media types.
    /// </summary>
    /// <param name="request">The <see cref="RiRequest"/> object containing the request details.</param>
    /// <returns>The negotiated content type for the response.</returns>
    private string NegotiateContentType(RiRequest request)
    {
        var acceptHeader = request.Headers.GetValueOrDefault("Accept", "text/html");
        var acceptedTypes = acceptHeader.Split(',')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t));

        // Find the first supported type that matches the client's preferences
        foreach (var acceptedType in acceptedTypes)
        {
            if (_supportedMediaTypes.ContainsKey(acceptedType))
            {
                return acceptedType;
            }
        }

        // If no match, use the default content type
        return GetContentType(request.Path);
    }

    /// <summary>
    /// Serializes the response object into the specified content type.
    /// </summary>
    /// <param name="result">The object to serialize.</param>
    /// <param name="contentType">The content type to use for serialization.</param>
    /// <returns>The serialized response as a string.</returns>
    private string SerializeResponse(object? result, string contentType)
    {
        if (result == null)
            return "";

        var serializer = _supportedMediaTypes.GetValueOrDefault(contentType);

        return serializer?.Invoke(result) ?? result.ToString() ?? "";
    }

    /// <summary>
    /// Adds a route to the router with the specified path template, handler, and HTTP method.
    /// </summary>
    /// <param name="pathTemplate">The path template for the route.</param>
    /// <param name="handler">The handler function for the route.</param>
    /// <param name="httpMethod">The HTTP method for the route (default: "GET").</param>
    public void AddRoute(string pathTemplate, Func<RiRequest, RiResponse> handler, string httpMethod = "GET")
    {
        _router.AddRoute(pathTemplate, handler, httpMethod);
    }

    /// <summary>
    /// Adds a route to the router with the specified path template, response, and HTTP method.
    /// </summary>
    /// <param name="pathTemplate">The path template for the route.</param>
    /// <param name="response">The response to return for the route.</param>
    /// <param name="httpMethod">The HTTP method for the route (default: "GET").</param>
    /// <remarks>
    /// This method internally calls the <see cref="AddRoute(string, Func{RiRequest, RiResponse}, string)"/> method with a lambda function that returns the specified response.
    /// </remarks>
    public void AddRoute(string pathTemplate, RiResponse response, string httpMethod = "GET")
    {
        AddRoute(pathTemplate, Handler, httpMethod);
        return;

        RiResponse Handler(RiRequest _) => response;
    }

    /// <summary>
    /// Adds a route to the router with the specified path template, response body, HTTP method, and content type.
    /// </summary>
    /// <param name="pathTemplate">The path template for the route.</param>
    /// <param name="body">The response body for the route.</param>
    /// <param name="httpMethod">The HTTP method for the route (default: "GET").</param>
    /// <param name="contentType">The content type for the response (default: "text/html").</param>
    public void AddRoute(string pathTemplate, string body, string httpMethod = "GET", string contentType = "text/html")
    {
        RiResponse handler = new()
        {
            StatusCode = HttpStatusCode.OK,
            Body = body,
            ContentType = contentType,
            ContentLength = Encoding.UTF8.GetByteCount(body)
        };
        AddRoute(pathTemplate, handler, httpMethod);
    }

    /// <summary>
    /// Adds an asynchronous route to the router with the specified path template, handler, HTTP method, and optional middleware attributes.
    /// </summary>
    /// <param name="pathTemplate">The path template for the route.</param>
    /// <param name="handler">The asynchronous handler function for the route.</param>
    /// <param name="httpMethod">The HTTP method for the route (default: "GET").</param>
    /// <param name="middlewareAttributes">Optional middleware attributes to apply to the route.</param>
    private void AddRoute(string pathTemplate, Func<RiRequest, Task<RiResponse>> handler, string httpMethod = "GET", IEnumerable<MiddlewareAttribute>? middlewareAttributes = null)
    {
        _router.AddRoute(pathTemplate, handler, httpMethod, middlewareAttributes);
    }

    /// <summary>
    /// Maps a controller to the router with the specified controller factory, route prefix, and optional middleware attributes.
    /// </summary>
    /// <typeparam name="TController">The type of the controller to map.</typeparam>
    /// <param name="controllerFactory">Optional factory function to create the controller instance (default: null).</param>
    /// <param name="routePrefix">Optional route prefix to apply to all routes of the controller (default: "").</param>
    public void MapController<TController>(Func<TController>? controllerFactory = null, string routePrefix = "") where TController : new()
    {
        var controllerType = typeof(TController);
        var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        foreach (var method in methods)
        {
            // Check for RiGet, RiPost, etc. attributes
            var attribute = method.GetCustomAttributes()
                .FirstOrDefault(attr => attr.GetType().Name.StartsWith("Ri", StringComparison.OrdinalIgnoreCase) &&
                                        attr.GetType().Name.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase));

            if (attribute != null)
            {
                var httpMethod =
                    attribute.GetType().Name
                        .Substring(2, attribute.GetType().Name.Length - 11);
                
                var routeTemplate = attribute.GetType().GetProperty("Route")?.GetValue(attribute) as string ?? method.Name;

                // Combine route prefix, controller name, and the route from the attribute:
                routeTemplate = routePrefix + "/" + routeTemplate; 
                
                // Add the route
                AddRoute(routeTemplate, CreateControllerAction(method, controllerFactory!() ?? new TController()), httpMethod, method.GetCustomAttributes<MiddlewareAttribute>());
            }
        }
    }

    /// <summary>
    /// Creates a controller action function that handles the invocation of a controller method with the specified method information and controller instance.
    /// </summary>
    /// <param name="methodInfo">The method information of the controller method.</param>
    /// <param name="controllerInstance">The instance of the controller.</param>
    /// <returns>The controller action function.</returns>
    private Func<RiRequest, Task<RiResponse>> CreateControllerAction(MethodInfo methodInfo, object controllerInstance)
    {
        var parameters = methodInfo.GetParameters();
        return async request =>
        {
            // --- Handle [FromBody] Parameters ---
            var actionParameters = parameters.Select(p =>
            {
                if (p.GetCustomAttribute<FromBodyAttribute>() != null)
                {
                    // Parameter marked with [FromBody]
                    try
                    {
                        // Attempt to deserialize from request body based on Content-Type
                        if (!request.Headers.TryGetValue("Content-Type", out var contentType))
                            return JsonSerializer.Deserialize(request.Body, p.ParameterType);
                        
                        if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
                        {
                            return JsonSerializer.Deserialize(request.Body, p.ParameterType);
                        }
                        if (contentType.Contains("application/xml", StringComparison.OrdinalIgnoreCase))
                        {
                            using var reader = new StringReader(request.Body);
                            return new XmlSerializer(p.ParameterType).Deserialize(reader);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deserializing Body: {ex.Message}");
                    }
                }

                return request; 
            }).ToArray();
            
            // Invoke the controller method
            var result =
                methodInfo.Invoke(controllerInstance, actionParameters);

            var contentType = NegotiateContentType(request);
            
            switch (result)
            {
                case Task<RiResponse> task:
                    var awaitedResponse = await task;
                    awaitedResponse.Body = SerializeResponse(awaitedResponse.Body, contentType);
                    awaitedResponse.ContentType = contentType;
                    awaitedResponse.ContentLength = Encoding.UTF8.GetByteCount(awaitedResponse.Body);
                    return awaitedResponse;
                case RiResponse response:
                    response.Body = SerializeResponse(response.Body, contentType);
                    response.ContentType = contentType;
                    response.ContentLength = Encoding.UTF8.GetByteCount(response.Body);
                    return response;
                default:
                {
                    var responseBody = result?.ToString() ?? "";

                    return new RiResponse(SerializeResponse(responseBody, contentType))
                    {
                        StatusCode = HttpStatusCode.OK,
                        ContentType = contentType
                    };
                }
            }
        };
    }
    
    /// <summary>
    /// Adds a global middleware to the middleware manager.
    /// </summary>
    /// <param name="middleware">The middleware to add.</param>
    /// <remarks>
    /// This method adds the specified middleware to the global middleware list of the middleware manager.
    /// The middleware will be executed for all incoming requests.
    /// </remarks>
    public void AddMiddleware(IMiddleware middleware)
    {
        _middlewareManager.AddGlobalMiddleware(middleware);
    }
}