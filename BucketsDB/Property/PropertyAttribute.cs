namespace BucketsDB.Property;

[AttributeUsage(AttributeTargets.Property)]
public class PropertyAttribute : Attribute
{
    public PropertyAttribute(string comments) => Comments = comments;
    public string Comments { get; }
}