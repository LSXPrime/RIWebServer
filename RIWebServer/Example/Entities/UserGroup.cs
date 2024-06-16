using RIWebServer.Attributes.ORM;

namespace RIWebServer.Example.Entities;


public class UserGroup
{
    [PrimaryKey]
    public int Id { get; set; }
    public string Name { get; set; }
    
    [NotMapped]
    public List<UserData> Users { get; set; } = [];
}