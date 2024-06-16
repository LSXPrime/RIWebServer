using RIWebServer.Attributes.ORM;

namespace RIWebServer.Example.Entities;

public class UserData
{
    [PrimaryKey]
    public int Id { get; set; }
    public string Name { get; set; }
    public string Password { get; set; }
    
    [ForeignKey("UserGroup")]
    public int UserGroupId { get; set; }
    [NotMapped]
    public UserGroup UserGroup { get; set; }
}