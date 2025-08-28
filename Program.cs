var builder = WebApplication.CreateBuilder(args);

var jsonRoot = builder.Configuration["JsonRootFolder"];
if (string.IsNullOrEmpty(jsonRoot))
{
    throw new Exception("JsonRootFolder configuration is required");
}

var aliasFile = builder.Configuration["TypeAliasFile"];
if (string.IsNullOrEmpty(aliasFile))
{
    throw new Exception("TypeAliasFile configuration is required");
}


var typeAliasProvider = new TypeAliasProvider(aliasFile);
builder.Services.AddControllers().AddNewtonsoftJson();

builder.Services.AddSingleton<MemoryRepository>();
builder.Services.AddSingleton<IMemoryRepository>(sp => sp.GetRequiredService<MemoryRepository>());
builder.Services.AddSingleton<JsonIndexer>(sp =>
{
    var repo = sp.GetRequiredService<MemoryRepository>();
    return new JsonIndexer(jsonRoot, repo, typeAliasProvider);
});

builder.Services.AddHostedService<IndexingHostedService>();
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();
app.Run();