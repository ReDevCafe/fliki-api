using Newtonsoft.Json;

public class TypeAliasProvider
{
    public Dictionary<string, List<string>> Aliases { get; private set; } = new();

    public TypeAliasProvider(string aliasFilePath)
    {
        if (File.Exists(aliasFilePath))
        {
            var json = File.ReadAllText(aliasFilePath);
            Aliases = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json) ?? new();
        }
    }

    public string NormalizeType(string rawType)
    {
        foreach (var kv in Aliases)
        {
            if(kv.Value.Any(alias => alias.Equals(rawType, StringComparison.OrdinalIgnoreCase)))
                return kv.Key;
        }

        return rawType;
    }
}