using System.Collections.Concurrent;

public class MemoryRepository : IMemoryRepository
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DataEntry>> _entriesType = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DataEntry>> _entriesById = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<ReferenceLocation>> _backReferences = new();

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, DataEntry>> EntriesType =>
    _entriesType.ToDictionary(k => k.Key, v => (IReadOnlyDictionary<string, DataEntry>)v.Value);

    public IReadOnlyDictionary<string, IReadOnlyList<ReferenceLocation>> BackReferences =>
    _backReferences.ToDictionary(k => k.Key, v => (IReadOnlyList<ReferenceLocation>)v.Value.ToList());

    public void AddEntry(string type, string id, DataEntry entry)
    {
        var dictionary = _entriesType.GetOrAdd(type, _ => new ConcurrentDictionary<string, DataEntry>());
        dictionary[id] = entry;

        var byIdDict = _entriesById.GetOrAdd(id, _ => new ConcurrentDictionary<string, DataEntry>());
        byIdDict[type] = entry;
    }

    public void AddBackReference(string referencedId, ReferenceLocation location)
    {
        var bag = _backReferences.GetOrAdd(referencedId, _ => new ConcurrentBag<ReferenceLocation>());
        bag.Add(location);
    }

    public bool TryGetById(string id, out DataEntry? entry)
    {
        entry = null;
        if (_entriesById.TryGetValue(id, out var byIdDict))
        {
            foreach (var kv in byIdDict)
            {
                entry = kv.Value;
                return true;
            }
        }
        return false;
    }

    public bool TryGet(string type, string id, out DataEntry? entry)
    {
        entry = null;
        if (_entriesType.TryGetValue(type, out var dict) && dict.TryGetValue(id, out var e))
        {
            entry = e;
            return true;
        }
        return false;
    }

    public bool TryGetAll(string type, out IEnumerable<DataEntry>? entries)
    {
        if (_entriesType.TryGetValue(type, out var dict))
        {
            entries = dict.Values;
            return dict.Count > 0;
        }

        entries = Enumerable.Empty<DataEntry>();
        return false;
    }
}