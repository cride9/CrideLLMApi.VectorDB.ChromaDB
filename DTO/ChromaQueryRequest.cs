using System.Text.Json.Serialization;

namespace CrideLLMApi.VectorDB.ChromaDB.DTO;

internal sealed record ChromaQueryRequest
{
    [JsonPropertyName("query_embeddings")]
    public required float[][] QueryEmbeddings { get; init; }

    [JsonPropertyName("n_results")]
    public int NResults { get; init; } = 10;

    [JsonPropertyName("include")]
    public string[] Include { get; init; } =
    [
        "distances",
        "documents",
        "metadatas"
    ];
}