namespace RIWebServer.Attributes.ORM;

[AttributeUsage(AttributeTargets.Property)]
public class ForeignKeyAttribute(string relatedTable) : Attribute
{
    public string RelatedTable { get; } = relatedTable;
}