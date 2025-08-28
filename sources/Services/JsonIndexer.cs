using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class JsonIndexer
{
    private readonly string _rootDir;
    private readonly MemoryRepository _repository;
    private readonly TypeAliasProvider _typeAliasProvider;

    public JsonIndexer(string rootDir, MemoryRepository repository, TypeAliasProvider typeAliasProvider)
    {
        _rootDir = rootDir;
        _repository = repository;
        _typeAliasProvider = typeAliasProvider;
    }

    public void BuildIndex()
    {
        var allEntries = LoadAllEntries();
        foreach (var entry in allEntries)
            _repository.AddEntry(_typeAliasProvider.NormalizeType(entry.Type), entry.Id, entry);

        BuildReferences(allEntries.ToList());
    }

    private IEnumerable<DataEntry> LoadAllEntries()
    {
        var files = Directory.EnumerateFiles(_rootDir, "*.json", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            using var reader = new StreamReader(file);
            using var jsonReader = new JsonTextReader(reader) { SupportMultipleContent = true };

            while (jsonReader.Read())
            {
                if (jsonReader.TokenType == JsonToken.Comment) continue;
                var token = JToken.ReadFrom(jsonReader);

                if (token is JArray arr)
                {
                    foreach (var element in arr)
                    {
                        foreach (var entry in ExtractEntriesFromTopLevel(element))
                            yield return entry;
                    }
                }
                else
                {
                    foreach (var entry in ExtractEntriesFromTopLevel(token))
                        yield return entry;
                }
            }
        }
    }

    private IEnumerable<DataEntry> ExtractEntriesFromTopLevel(JToken top)
    {
        var type = top["Name"] ?? top["name"];
        if (type == null) yield break;

        var typeStr = _typeAliasProvider.NormalizeType(type.ToString());
        var props = top["Properties"] ?? top["properties"];

        if (props != null)
        {
            var map = props["m_dataMap"] as JArray;
            if (map != null)
            {
                foreach (var kv in map)
                {
                    var key = kv["Key"];
                    if (key is JObject keyObj && keyObj["Name"] != null) key = keyObj["Name"];
                    var value = kv["Value"] as JObject;

                    if (value == null) continue;

                    var id = value["Id"] ?? key;
                    if (id == null || string.IsNullOrEmpty(id.ToString())) continue;
                    yield return new DataEntry
                    {
                        Type = typeStr,
                        Id = id.ToString(),
                        Raw = value.DeepClone() as JObject ?? new JObject()
                    };
                }
            }
        }

        var idDirect = top["Properties"]?["ID"] ?? top["ID"];
        if (idDirect != null)
        {
            yield return new DataEntry
            {
                Type = type.ToString(),
                Id = idDirect.ToString(),
                Raw = top.DeepClone() as JObject ?? new JObject()
            };
        }

        var name = top["Name"]?.ToString();
        var classToken = top["Class"];
        // TODO: Handle name and classToken
    }

    private void BuildReferences(List<DataEntry> allEntries)
    {
        var idSet = new HashSet<string>(allEntries.Select(e => e.Id));

        foreach (var entry in allEntries)
        {
            foreach (var token in entry.Raw.DescendantsAndSelf())
            {
                if (token is JValue val && val.Type == JTokenType.String)
                {
                    var str = val.ToString();
                    if (idSet.Contains(str))
                    {
                        var path = token.Path;
                        _repository.AddBackReference(
                            str,
                            new ReferenceLocation(entry.Type, entry.Id, path)
                        );
                    }
                }
                else if (token is JArray array)
                {
                    foreach (var child in array.Children<JValue>())
                    {
                        if (child.Type == JTokenType.String)
                        {
                            var s = child.ToString();
                            if (idSet.Contains(s))
                                _repository.AddBackReference(
                                    s,
                                    new ReferenceLocation(entry.Type, entry.Id, child.Path)
                                );
                        }
                    }
                }
            }
        }
    }

}