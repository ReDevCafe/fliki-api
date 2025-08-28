public class IndexingHostedService : IHostedService
{
    private readonly JsonIndexer _indexer;

    public IndexingHostedService(JsonIndexer indexer)
    {
        _indexer = indexer;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _indexer.BuildIndex();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}