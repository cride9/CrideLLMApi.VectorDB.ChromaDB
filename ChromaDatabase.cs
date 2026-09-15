using CrideLLMApi.Api;
using CrideLLMApi.DTO;
using CrideLLMApi.VectorDB.ChromaDB.DTO;

namespace CrideLLMApi.VectorDB.ChromaDB;

public sealed class ChromaDatabase : IVectorDatabase
{
    private readonly ChromaTransport _transport;
    private readonly ChromaOptions _options;
    private readonly ChromaServerManager _serverManager;

    public ChromaDatabase(CrideApi api, ProviderInfo providerInfo)
    {
        _options = new ChromaOptions
        {
            EndPoint = providerInfo.Extensions.VectorDBEndPoint ?? new Uri("http://127.0.0.1:8000"),
            ApiKey = providerInfo.Extensions.VectorDBApiKey,
            AutoStart = providerInfo.Extensions.AutoStartVectorDB,
            RuntimePath = providerInfo.Extensions.VectorDBRuntimePath
                ?? Path.Combine(Environment.CurrentDirectory, ".cride", "chromadb"),
            DataPath = providerInfo.Extensions.VectorDBDataPath
                ?? Path.Combine(Environment.CurrentDirectory, ".cride", "chromadb", "data")
        };

        _serverManager = new ChromaServerManager( );
        _transport = new ChromaTransport(_options);
    }

    /// <summary>
    /// Gets or creates a collection with the specified name. If the collection does not exist, it will be created. If it already exists, the existing collection will be returned.
    /// </summary>
    /// <param name="name">The name of the collection to get or create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<IVectorCollection> GetOrCreateCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await EnsureServerAsync(cancellationToken);

        var path = BuildCollectionsPath();

        var response =
            await _transport.PostAsync<ChromaCreateCollectionRequest, ChromaCollectionDto>(path,
                new ChromaCreateCollectionRequest
                {
                    Name = name,
                    GetOrCreate = true
                },
                cancellationToken);

        return new ChromaCollection(response.Id, response.Name, _transport, _options, _serverManager);
    }

    /// <summary>
    /// Gets a list of all collection names in the database.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<IReadOnlyList<string>> GetCollectionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureServerAsync(cancellationToken);

        var collections = await _transport.GetAsync<List<ChromaCollectionDto>>(BuildCollectionsPath(), cancellationToken);

        return collections.Select(x => x.Name)
            .ToArray();
    }

    /// <summary>
    /// Deletes a collection with the specified name from the database. If the collection does not exist, no action is taken.
    /// </summary>
    /// <param name="name">The name of the collection to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await EnsureServerAsync(cancellationToken);

        var escaped = Uri.EscapeDataString(name);

        await _transport.DeleteAsync($"{BuildCollectionsPath()}/{escaped}", cancellationToken);
    }

    private Task EnsureServerAsync(CancellationToken cancellationToken)
    {
        return _serverManager.EnsureRunningAsync(_options, cancellationToken);
    }

    /// <summary>
    /// Builds the API path for accessing collections in the Chroma database, including the tenant and database names. The tenant and database names are URL-encoded to ensure they are safe for use in a URL.
    /// </summary>
    private string BuildCollectionsPath()
    {
        var tenant = Uri.EscapeDataString(_options.Tenant);
        var database = Uri.EscapeDataString(_options.Database);

        return $"api/v2/tenants/{tenant}/databases/{database}/collections";
    }
}