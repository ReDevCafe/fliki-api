using Microsoft.AspNetCore.Mvc;

[ApiController]
public class ApiController : ControllerBase
{
    private readonly MemoryRepository _repository;

    public ApiController(MemoryRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("api/id/{id}")]
    public IActionResult GetFromId(string id)
    {
        if (_repository.TryGetById(id, out var entry))
        {
            var definition = entry!.Raw.ToObject<Dictionary<string, object>>();

            List<object> references;
            if (_repository.BackReferences.TryGetValue(id, out var refs))
                references = refs.Select(r => (object)new { r.FilePath, r.ReferencingId, r.JsonPath }).ToList();
            else references = new List<object>();

            return Ok(new { definition, references });
        }

        return NotFound(new { message = $"Entry with ID '{id}' not found." });
    }

    [HttpGet("api/{type}/{id}")]
    public IActionResult Get(string type, string id)
    {
        if (_repository.TryGet(type, id, out var entry))
        {
            var definition = entry!.Raw.ToObject<Dictionary<string, object>>();

            List<object> references;
            if (_repository.BackReferences.TryGetValue(id, out var refs))
                references = refs.Select(r => (object)new { r.FilePath, r.ReferencingId, r.JsonPath }).ToList();
            else references = new List<object>();

            return Ok(new { definition, references });
        }

        return NotFound(new { message = $"Entry with ID '{id}' and type '{type}' not found." });
    }
}