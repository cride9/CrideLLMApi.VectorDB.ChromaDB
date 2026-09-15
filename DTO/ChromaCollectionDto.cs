using System.Text.Json.Serialization;

namespace CrideLLMApi.VectorDB.ChromaDB.DTO;

internal sealed record ChromaCollectionDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; init; }

    [JsonPropertyName("tenant")]
    public string? Tenant { get; init; }

    [JsonPropertyName("database")]
    public string? Database { get; init; }
}