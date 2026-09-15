using CrideLLMApi.Api;
using CrideLLMApi.DTO;
using CrideLLMApi.VectorDB.ChromaDB.DTO;

namespace CrideLLMApi.VectorDB.ChromaDB;

public sealed class ChromaDatabase : IVectorDatabase
{
    private readonly ChromaTransport _transport;
    private readonly ChromaOptions _options;

    public ChromaDatabase(
        CrideApi api,
        ProviderInfo providerInfo)
    {
        var endPoint =
            providerInfo.Extensions.VectorDBEndPoint
            ?? throw new InvalidOperationException(
                "VectorDBEndPoint must be configured when using ChromaDB.");

        _options = new ChromaOptions
        {
            EndPoint = endPoint,

            ApiKey =
                providerInfo.Extensions.VectorDBApiKey
        };

        _transport =
            new ChromaTransport(
                _options);
    }

    /// <summary>
    /// Gets or creates a collection with the specified name. If the collection does not exist, it will be created. If it already exists, the existing collection will be returned.
    /// </summary>
    /// <param name="name">The name of the collection to get or create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<IVectorCollection> GetOrCreateCollectionAsync(string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var path =
            BuildCollectionsPath();

        var response =
            await _transport.PostAsync<
                ChromaCreateCollectionRequest,
                ChromaCollectionDto>(
                path,
                new ChromaCreateCollectionRequest
                {
                    Name = name,
                    GetOrCreate = true
                },
                cancellationToken);

        return new ChromaCollection(
            response.Id,
            response.Name,
            _transport,
            _options);
    }

    /// <summary>
    /// Gets a list of all collection names in the database.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<IReadOnlyList<string>> GetCollectionsAsync(
        CancellationToken cancellationToken = default)
    {
        var collections =
            await _transport.GetAsync<
                List<ChromaCollectionDto>>(
                BuildCollectionsPath(),
                cancellationToken);

        return collections
            .Select(x => x.Name)
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

        var escaped =
            Uri.EscapeDataString(name);

        await _transport.DeleteAsync(
            $"{BuildCollectionsPath()}/{escaped}",
            cancellationToken);
    }

    /// <summary>
    /// Builds the API path for accessing collections in the Chroma database, including the tenant and database names. The tenant and database names are URL-encoded to ensure they are safe for use in a URL.
    /// </summary>
    private string BuildCollectionsPath()
    {
        var tenant =
            Uri.EscapeDataString(
                _options.Tenant);

        var database =
            Uri.EscapeDataString(
                _options.Database);

        return
            $"api/v2/tenants/{tenant}/databases/{database}/collections";
    }
}