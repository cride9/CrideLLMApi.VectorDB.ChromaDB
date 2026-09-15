using System.Text.Json.Serialization;

namespace CrideLLMApi.VectorDB.ChromaDB.DTO;

internal sealed record ChromaCreateCollectionRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("get_or_create")]
    public bool GetOrCreate { get; init; } = true;

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; init; }
}