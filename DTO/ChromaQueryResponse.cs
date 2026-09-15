using System.Text.Json.Serialization;

namespace CrideLLMApi.VectorDB.ChromaDB.DTO;

internal sealed record ChromaQueryResponse
{
    [JsonPropertyName("ids")]
    public string[][] Ids { get; init; } = [];

    [JsonPropertyName("distances")]
    public double?[][]? Distances { get; init; }

    [JsonPropertyName("documents")]
    public string?[][]? Documents { get; init; }

    [JsonPropertyName("metadatas")]
    public Dictionary<string, object?>?[][]? Metadatas { get; init; }
}