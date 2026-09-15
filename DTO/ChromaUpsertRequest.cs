using System.Text.Json.Serialization;

namespace CrideLLMApi.VectorDB.ChromaDB.DTO;

internal sealed record ChromaUpsertRequest
{
    [JsonPropertyName("ids")]
    public required string[] Ids { get; init; }

    [JsonPropertyName("embeddings")]
    public required float[][] Embeddings { get; init; }

    [JsonPropertyName("documents")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string?[]? Documents { get; init; }

    [JsonPropertyName("metadatas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>?[]? Metadatas { get; init; }
}