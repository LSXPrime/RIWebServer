using RIWebServer.Authentication.Entities;
using RIWebServer.ORM;

namespace RIWebServer.Authentication.Database;

public abstract class AuthenticationDbContext : DbContext
{
    protected AuthenticationDbContext(string connectionString) : base(connectionString)
    {
        Users = new DbSet<User>(this);
    }

    public DbSet<User> Users { get; set; }
}