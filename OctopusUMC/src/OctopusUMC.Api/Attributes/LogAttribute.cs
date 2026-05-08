namespace OctopusUMC.Api.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class LogAttribute(string title) : Attribute
{
    public string Title { get; } = title;
}
