public interface IMemoryRepository
{
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, DataEntry>> EntriesType { get; }
    IReadOnlyDictionary<string, IReadOnlyList<ReferenceLocation>> BackReferences { get; }

    bool TryGet(string type, string id, out DataEntry? entry);
    bool TryGetById(string id, out DataEntry? entry);
}
