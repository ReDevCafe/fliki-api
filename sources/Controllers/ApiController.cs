using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[ApiController]
public class ApiController : ControllerBase
{
    private readonly MemoryRepository _repository;
    private readonly IMemoryCache _memoryCache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _buildLocks = new();

    private const string KeyIdPrefix = ":id";
    private const string KeyPrecisePrefix = ":precise";
    private const string KeyTypePrefix = ":type";

    public ApiController(MemoryRepository repository, IMemoryCache memoryCache)
    {
        _repository = repository;
        _memoryCache = memoryCache;
    }

    [HttpGet("api/id/{id}")]
    public async Task<IActionResult> GetFromId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("Id cannot be empty");

        var cacheKey = KeyIdPrefix + id;

        if (_memoryCache.TryGetValue<SerializedPayload>(cacheKey, out var cached) && cached != null)
        {
            var ifNoneMatch = Request.Headers["If-None-Match"].FirstOrDefault();
            if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == cached.ETag)
                return StatusCode(304);

            Response.Headers["ETag"] = cached.ETag;
            return File(cached.Bytes, "application/json; charset=utf-8");
        }

        var payload = await GetOrBuildAsync(cacheKey, () => BuildSingleEntryPayloadById(id));
        if (payload == null)
            return NotFound();

        var ifNoneMatch2 = Request.Headers["If-None-Match"].FirstOrDefault();
        if (!string.IsNullOrEmpty(ifNoneMatch2) && ifNoneMatch2 == payload.ETag)
            return StatusCode(304);

        Response.Headers["ETag"] = payload.ETag;
        return File(payload.Bytes, "application/json; charset=utf-8");
    }

    [HttpGet("api/{type}/{id}")]
    public async Task<IActionResult> GetPrecise(string type, string id)
    {
        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id))
            return BadRequest("Type and Id cannot be empty");

        var cacheKey = KeyPrecisePrefix + type + ":" + id;

        if (_memoryCache.TryGetValue<SerializedPayload>(cacheKey, out var cached) && cached != null)
        {
            var ifNoneMatch = Request.Headers["If-None-Match"].FirstOrDefault();
            if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == cached.ETag)
                return StatusCode(304);

            Response.Headers["ETag"] = cached.ETag;
            return File(cached.Bytes, "application/json; charset=utf-8");
        }

        var payload = await GetOrBuildAsync(cacheKey, () => BuildSingleEntryPayloadByTypeAndId(type, id));
        if (payload == null)
            return NotFound();

        var ifNoneMatch2 = Request.Headers["If-None-Match"].FirstOrDefault();
        if (!string.IsNullOrEmpty(ifNoneMatch2) && ifNoneMatch2 == payload.ETag)
            return StatusCode(304);

        Response.Headers["ETag"] = payload.ETag;
        return File(payload.Bytes, "application/json; charset=utf-8");
    }

    [HttpGet("api/type/{type}")]
    public async Task<IActionResult> GetAllFromType(string type)
    {
        if (string.IsNullOrEmpty(type))
            return BadRequest("Type cannot be empty");

        var cacheKey = KeyTypePrefix + type;

        if (_memoryCache.TryGetValue<SerializedPayload>(cacheKey, out var cached) && cached != null)
        {
            var ifNoneMatch = Request.Headers["If-None-Match"].FirstOrDefault();
            if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == cached.ETag)
                return StatusCode(304);

            Response.Headers["ETag"] = cached.ETag;
            return File(cached.Bytes, "application/json; charset=utf-8");
        }

        if (!_repository.TryGetAll(type, out var entries) || entries == null || entries.Count() == 0)
            return NotFound();

        var payload = await GetOrBuildAsync(cacheKey, () => BuildListPayload(entries.ToList()));
        if (payload == null)
            return NotFound();

        var ifNoneMatch2 = Request.Headers["If-None-Match"].FirstOrDefault();
        if (!string.IsNullOrEmpty(ifNoneMatch2) && ifNoneMatch2 == payload.ETag)
            return StatusCode(304);

        Response.Headers["ETag"] = payload.ETag;
        return File(payload.Bytes, "application/json; charset=utf-8");
    }

    private async Task<SerializedPayload?> GetOrBuildAsync(string cacheKey, Func<SerializedPayload?> buildFunc)
    {
        var semaphore = _buildLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        try
        {
            if (_memoryCache.TryGetValue<SerializedPayload>(cacheKey, out var cached))
                return cached;

            var payload = await Task.Run(buildFunc);

            if (payload != null)
            {
                var options = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(120))
                    .SetSize(payload.Bytes.Length);
                _memoryCache.Set(cacheKey, payload, options);
            }

            return payload;
        }
        finally
        {
            semaphore.Release();
            _buildLocks.TryRemove(cacheKey, out _);
        }
    }

    private SerializedPayload? BuildSingleEntryPayloadById(string id)
    {
        if (!_repository.TryGetById(id, out var entry) || entry == null)
            return null;

        return BuildSingleEntryPayload(entry);
    }

    private SerializedPayload? BuildSingleEntryPayloadByTypeAndId(string type, string id)
    {
        if (!_repository.TryGet(type, id, out var entry) || entry == null)
            return null;

        return BuildSingleEntryPayload(entry);
    }

    private SerializedPayload BuildSingleEntryPayload(DataEntry entry)
    {
        using (var memstream = new MemoryStream())
        using (var streamWriter = new StreamWriter(memstream, Encoding.UTF8, 8192, true))
        using (var jsonWriter = new JsonTextWriter(streamWriter))
        {
            var serializer = new JsonSerializer();

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("definition");
            if (entry.Raw is JToken token)
                token.WriteTo(jsonWriter);
            else
                serializer.Serialize(jsonWriter, entry.Raw);

            jsonWriter.WritePropertyName("references");
            jsonWriter.WriteStartArray();

            if (_repository.BackReferences != null && _repository.BackReferences.TryGetValue(entry.Id, out var references) && references != null)
            {
                foreach (var r in references)
                {
                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("filepath"); jsonWriter.WriteValue(r.FilePath); /// TODO: Change it to Type, and give another row for name
                    jsonWriter.WritePropertyName("id"); jsonWriter.WriteValue(r.ReferencingId);
                    jsonWriter.WritePropertyName("jsonPath"); jsonWriter.WriteValue(r.JsonPath);
                    jsonWriter.WriteEndObject();
                }
            }

            jsonWriter.WriteEndArray();
            jsonWriter.WriteEndObject();

            jsonWriter.Flush();
            streamWriter.Flush();

            var bytes = memstream.ToArray();
            var etag = PayloadUtilities.ComputeETag(bytes);
            return new SerializedPayload { Bytes = bytes, ETag = etag };
        }
    }

    private SerializedPayload BuildListPayload(List<DataEntry> entries)
    {
        using (var memstream = new MemoryStream())
        using (var streamWriter = new StreamWriter(memstream, Encoding.UTF8, 8192, true))
        using (var jsonWriter = new JsonTextWriter(streamWriter))
        {
            var serializer = new JsonSerializer();

            jsonWriter.WriteStartArray();
            foreach (var entry in entries)
            {
                jsonWriter.WriteStartObject();
                jsonWriter.WritePropertyName("definition");
                if (entry.Raw is JToken token)
                    token.WriteTo(jsonWriter);
                else
                    serializer.Serialize(jsonWriter, entry.Raw);
                jsonWriter.WriteEndObject();
            }
            jsonWriter.WriteEndArray();

            jsonWriter.Flush();
            streamWriter.Flush();

            var bytes = memstream.ToArray();
            var etag = PayloadUtilities.ComputeETag(bytes);
            return new SerializedPayload { Bytes = bytes, ETag = etag };
        }
    }
}