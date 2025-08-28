using Newtonsoft.Json.Linq;

public class DataEntry
{
    public string Type { get; init; } = "";
    public string Id { get; init; } = "";
    public JObject Raw { get; init; } = new JObject();
}