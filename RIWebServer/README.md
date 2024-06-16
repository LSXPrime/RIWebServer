# LSXPrime (RI) Web Server

## Overview

RI WebServer is a lightweight, cross-platform web server built using C#. It's designed to be easy to use and extend, allowing you to quickly create web applications, APIs.

But let's forget all the fancy stuff for now—this is just a simple web server for my portfolio. Still, it’s pretty handy. I'll use it to create APIs for some personal projects and might tweak it later.


## Features

* **Routing:** Define routes with support for route parameters, different HTTP methods, and flexible route mapping to controller actions.
* **Middleware:** Add custom middleware to handle cross-cutting concerns like authentication, logging, and more, both globally and at the route level.
* **Sessions:**  Manage user sessions to store data.
* **Content Negotiation:** Serve different content types (JSON, XML, HTML) based on client requests.
* **ORM:**  Basic Object-Relational Mapping (ORM) functionalities for easier database interactions with sqlite using code-first approach.
* **Authentication:** Built in JWT Authentication with user registration and login

## Getting Started

### 1. Basic Server Setup (Program.cs)

```csharp
using RIWebServer;
using RIWebServer.Requests;

namespace MyWebApp;

internal static class Program
{
    private static async Task Main()
    {
        var server = new RiWebServer(8080, "127.0.0.1"); // Listen on port 8080 and bind to localhost

        // Add a route to the server
        server.AddRoute("/", _ => new RiResponse("<h1>Hello from RIWebServer!</h1>"));
        
        // Add a global middleware to the server
        server.AddMiddleware(new MyMiddleware());

        // Map the controller to the server
        server.MapController(() => new MyController(), "my-controller");


        await server.Start(); 
    }
}
```

### 2. Run the Server

```bash
dotnet run
```

Now navigate to `http://localhost:8080/` in your web browser, and you should see the greeting message.

## Routing

RIWebServer uses an attribute based routing system to map HTTP requests to specific controller actions.

```csharp
using RIWebServer;
using RIWebServer.Requests;
using RIWebServer.Attributes.Http;

// ... (other namespaces)

public class MyController
{
    [RiGet] // Match GET requests to the root path
    public RiResponse Index()
    {
        return new RiResponse("<h1>This is the index page.</h1>");
    }

    [RiGet("hello/{name}")] // Match GET requests to /hello/{name}
    public RiResponse Greet(RiRequest request)
    {
        var name = request.RouteParams["name"]; 
        return new RiResponse($"<h1>Hello, {name}!</h1>");
    }
    
    [RiPost("login")] // Match POST requests to /login
    public RiResponse Login([FromBody] LoginRequest request)
    {
        // logic to handle user login
        // ...
        return new RiResponse("Login successful!");
    }
}

// In your Program.cs:
server.MapController(() => new MyController()); 
```

**Supported HTTP Methods:**

* `[RiGet]`
* `[RiPost]`
* `[RiPut]`
* `[RiDelete]`

### Route Parameters

You can define route parameters by enclosing them in curly braces `{}`.

```csharp
[RiGet("users/{id}")] 
public RiResponse GetUser(RiRequest request)
{
    var userId = int.Parse(request.RouteParams["id"]); 
    // ... Logic to fetch and return user data
}
```

## Middleware

Middleware allows you to inject code that executes before or after a request is handled by a route. It is useful for:

* **Authentication:** Verify user credentials before allowing access to protected resources.
* **Logging:**  Log incoming requests, response times, and errors.
* **Request/Response Modification:**  Modify headers, add data to the request, or transform the response.

### Creating Middleware

Implement the `IMiddleware` interface:

```csharp
using RIWebServer.Requests;
using RIWebServer.Middleware;

public class LoggingMiddleware : IMiddleware
{
    public async Task InvokeAsync(RiRequest request, RiResponse response, Func<Task> next)
    {
        Console.WriteLine($"Request: {request.Method} {request.Path}");

        // Call next middleware in the pipeline or the final request handler.
        await next(); 

        Console.WriteLine($"Response: {response.StatusCode}");
    }
}
```

### Registering Middleware

* **Global Middleware:**  Applied to all requests.

```csharp
server.AddMiddleware(new LoggingMiddleware());
```

* **Route-Specific Middleware:**  Applied to specific routes using the `[Middleware]` attribute.

```csharp
[RiGet("profile")]
[Middleware(typeof(AuthenticationMiddleware))] // Only applied to this route
public RiResponse GetProfile() 
{
  // ...
}
```

## Sessions

Sessions provide a way to store user-specific data across multiple requests.

### Accessing Session Data

```csharp
[RiGet("set-session")]
public RiResponse SetSessionData(RiRequest request)
{
  request.Session["username"] = "JohnDoe"; 
  return new RiResponse("Session data set!");
}

[RiGet("get-session")]
public RiResponse GetSessionData(RiRequest request)
{
  var username = request.Session["username"]; 
  // ...
}
```

### Session Management

The `SessionManager` handles session creation, retrieval, and cleanup (removing expired sessions).

### Configuration (SessionManager.cs)

```csharp
private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(20);  // Default session timeout
private const string SessionCookieName = "SESSION_ID"; // Default session cookie name
```

## Content Negotiation

RIWebServer supports content negotiation to serve responses in different formats.

### Supported Media Types

* `text/html`
* `application/json`
* `application/xml`
* `text/plain`

### Customizing Supported Media Types
You can customize the supported media types and their serializers in the RiWebServer class constructor:

```csharp
public RiWebServer(int port, string ipAddress = "")
{ 
    // ... Other code

    _supportedMediaTypes = new Dictionary<string, Func<object, string>>()
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
        // Add more supported types here if needed
    };
} 
```

### Negotiation

The `NegotiateContentType()` method determines the best content type to use based on:
* The `Accept` header sent by the client.
* The supported media types.

## ORM (Object-Relational Mapping)

RIWebServer provides a simple ORM to interact with SQLite databases.

### DbContext

Create a class that inherits from `DbContext`:

```csharp
using RIWebServer.ORM;

public class AppDbContext : DbContext // Inherit from AuthenticationDbContext to use authentication features and users table
{
    public AppDbContext(string connectionString) : base(connectionString) 
    {
        // Initialize your DbSets here 
        Products = new DbSet<Product>(this);
        Categories = new DbSet<Category>(this);
    }

    // Define DbSets for your entities
    public DbSet<Product> Products { get; set; } 
    public DbSet<Category> Categories { get; set; }
}
```

### DbSet

A `DbSet` represents a collection of entities of a specific type in the database. You can use it to query and manipulate data in a type-safe way.

RIWebServer's `DbSet` provides the following methods:

* `GetById(int id)`: Retrieves a single entity by its primary key.
* `GetAll()`:  Retrieves all entities from the table.
* `GetAllWithRelated()`: Retrieves all entities with their related entities
* `Add(TEntity entity)`:  Adds a new entity to the table.
* `Update(TEntity entity)`: Updates an existing entity.
* `Delete(int id)`:  Deletes an entity by its primary key.

### Data Annotations

Use data annotations from the `RIWebServer.Attributes.ORM` namespace to configure entities:

* `[PrimaryKey]` To set the field as the primary key.
* `[ForeignKey]` To use the field as foreign key to reference another table in one-to-many relationship.
* `[NotMapped]` To exclude the field from the database and set the navigation property.

### Example

```csharp
using System.Collections.Generic;
using RIWebServer.ORM;
using RIWebServer.Attributes.ORM;

// Entity class
using RIWebServer.Attributes.ORM;

// Product entity
public class Product
{
    [PrimaryKey]
    public int Id { get; set; } 

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    [ForeignKey("Categories")] // Foreign key to the Categories table
    public int CategoryId { get; set; } 

    // Not mapped to a database column and used as a navigation property
    [NotMapped]
    public Category Category { get; set; } 
}

// Category entity
public class Category
{
    [PrimaryKey]
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}

// Usage
var dbContext = new AppDbContext("Data Source=mydatabase.db"); 
dbContext.EnsureDatabaseCreated();

// Add a new category
var newCategory = new Category { Name = "Electronics" };
dbContext.Categories.Add(newCategory);
dbContext.SaveChanges();

// Add a new product
var newProduct = new Product { Name = "Laptop", Price = 1200, CategoryId = newCategory.Id };
dbContext.Products.Add(newProduct);
dbContext.SaveChanges(); 

var products = dbContext.Products.GetAll();
var productsByCategory = _dbContext?.Products.GetAllWithRelated()
            .Where(b => b.CategoryId == targetCategoryId).ToArray(); // where targetCategoryId is the categoryId you want to filter by
```

## Authentication

RIWebServer includes built-in support for JSON Web Token (JWT) authentication.

### Configuration (Program.cs)

```csharp
var authManager = new AuthenticationManager(dbContext, "your_secret_key_here"); // Initialize with your database context and secret key
```

### Authentication Middleware

The `AuthenticationMiddleware` handles:

* Extracting the JWT from the `Authorization` header.
* Validating the token's signature and expiration.
* Setting the `request.User` property if the token is valid.

### Using Authentication

```csharp
[RiGet("protected")]
[Middleware(typeof(AuthenticationMiddleware))]
public RiResponse ProtectedRoute(RiRequest request)
{
  if (request.User != null)
  {
      return new RiResponse($"Welcome, {request.User.Username}!"); 
  }
  // ... handle unauthorized access
}
```

### Authentication Manager

The `AuthenticationManager` provides methods for:

* `RegisterUser()`:  Registers a new user.
* `LoginUser()`:  Authenticates a user and generates a JWT.
* `GetUserById()`: Fetches a user by their ID.

Please refer to the source code for more detailed information on the JWT implementation and usage.

## Logging

* **Console Logging:**  The `LoggingMiddleware` provides basic console logging for requests and responses.
* **Custom Logging:** You can implement your own logging middleware or use a third-party logging library.

## Contributing

Contributions are welcome!
* Fork the repository.
* Create a new branch for your feature/fix.
* Make your changes and commit them with clear messages.
* Push your branch and submit a pull request.

## License

This project is licensed under the MIT License.
