using CrideLLMApi.VectorDB.ChromaDB.DTO;

namespace CrideLLMApi.VectorDB.ChromaDB;

public sealed class ChromaCollection : IVectorCollection
{
    private readonly string _id;
    private readonly ChromaTransport _transport;
    private readonly ChromaOptions _options;

    public string Name { get; }

    internal ChromaCollection(string id, string name, ChromaTransport transport, ChromaOptions options)
    {
        _id = id;
        Name = name;
        _transport = transport;
        _options = options;
    }

    /// <summary>
    /// Upserts a single vector record into the collection.
    /// </summary>
    /// <param name="record">The vector record to upsert.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task UpsertAsync(VectorRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        return UpsertAsync(
            [record],
            cancellationToken);
    }

    /// <summary>
    /// Upserts a batch of vector records into the collection.
    /// </summary>
    /// <param name="records">The vector records to upsert.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task UpsertAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        var items =
            records.ToArray();

        if (items.Length == 0)
        {
            throw new ArgumentException(
                "At least one vector record is required.",
                nameof(records));
        }

        foreach (var item in items)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                item.Id);

            if (item.Vector.Length == 0)
            {
                throw new ArgumentException(
                    $"Vector '{item.Id}' contains no dimensions.",
                    nameof(records));
            }
        }

        /*
         * Chroma requires all vectors in the collection
         * to have the same dimensionality.
         *
         * Catch obvious batch mismatches client-side.
         */
        var dimensions =
            items[0].Vector.Length;

        if (items.Any(
                x => x.Vector.Length != dimensions))
        {
            throw new ArgumentException(
                "All vectors in an upsert batch must have the same dimensions.",
                nameof(records));
        }

        var request =
            new ChromaUpsertRequest
            {
                Ids =
                    items
                        .Select(x => x.Id)
                        .ToArray(),

                Embeddings =
                    items
                        .Select(x => x.Vector)
                        .ToArray(),

                Documents =
                    items
                        .Select(x => x.Document)
                        .ToArray(),

                Metadatas =
                    items
                        .Select(x =>
                            x.Metadata.Count == 0
                                ? null
                                : new Dictionary<string, object?>(
                                    x.Metadata))
                        .ToArray()
            };

        await _transport.PostAsync(
            BuildRecordPath("upsert"),
            request,
            cancellationToken);
    }

    /// <summary>
    /// Searches the collection for the most similar vectors to the provided query vector.
    /// </summary>
    /// <param name="vector">The query vector.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(ReadOnlyMemory<float> vector, int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (vector.IsEmpty)
        {
            throw new ArgumentException(
                "Query vector cannot be empty.",
                nameof(vector));
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit));
        }

        var request =
            new ChromaQueryRequest
            {
                QueryEmbeddings =
                [
                    vector.ToArray()
                ],

                NResults = limit
            };

        var response =
            await _transport.PostAsync<
                ChromaQueryRequest,
                ChromaQueryResponse>(
                BuildRecordPath("query"),
                request,
                cancellationToken);

        /*
         * We send exactly one query embedding,
         * so result set [0] belongs to that query.
         */
        if (response.Ids.Length == 0)
            return [];

        var ids =
            response.Ids[0];

        var distances =
            response.Distances is { Length: > 0 }
                ? response.Distances[0]
                : null;

        var documents =
            response.Documents is { Length: > 0 }
                ? response.Documents[0]
                : null;

        var metadatas =
            response.Metadatas is { Length: > 0 }
                ? response.Metadatas[0]
                : null;

        var results =
            new List<VectorSearchResult>(
                ids.Length);

        for (var i = 0; i < ids.Length; i++)
        {
            results.Add(
                new VectorSearchResult
                {
                    Id = ids[i],

                    Distance =
                        distances is not null &&
                        i < distances.Length &&
                        distances[i] is double distance
                            ? distance
                            : double.NaN,

                    Document =
                        documents is not null &&
                        i < documents.Length
                            ? documents[i]
                            : null,

                    Metadata =
                        metadatas is not null &&
                        i < metadatas.Length &&
                        metadatas[i] is not null
                            ? metadatas[i]!
                            : new Dictionary<string, object?>()
                });
        }

        return results;
    }

    /// <summary>
    /// Deletes a vector record from the collection by its ID.
    /// </summary>
    /// <param name="id">The ID of the record to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var request =
            new ChromaDeleteRequest
            {
                Ids = [id]
            };

        await _transport.PostAsync(
            BuildRecordPath("delete"),
            request,
            cancellationToken);
    }

    private string BuildRecordPath(
        string operation)
    {
        var tenant =
            Uri.EscapeDataString(
                _options.Tenant);

        var database =
            Uri.EscapeDataString(
                _options.Database);

        var collectionId =
            Uri.EscapeDataString(
                _id);

        return
            $"api/v2/tenants/{tenant}/databases/{database}/collections/{collectionId}/{operation}";
    }
}