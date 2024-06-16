using RIWebServer.Authentication.Database;
using RIWebServer.Example.Entities;
using RIWebServer.ORM;

namespace RIWebServer.Example.Database;

public class AppDbContext : AuthenticationDbContext
{
    public AppDbContext(string connectionString) : base(connectionString)
    {
        UsersData = new DbSet<UserData>(this);
        UserGroups = new DbSet<UserGroup>(this);
    }

    public DbSet<UserData> UsersData { get; set; }
    public DbSet<UserGroup> UserGroups { get; set; }
}