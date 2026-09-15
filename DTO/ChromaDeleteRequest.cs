using System.Text.Json.Serialization;

namespace CrideLLMApi.VectorDB.ChromaDB.DTO;

internal sealed record ChromaDeleteRequest
{
    [JsonPropertyName("ids")]
    public required string[] Ids { get; init; }
}