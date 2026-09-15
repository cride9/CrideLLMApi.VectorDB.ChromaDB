namespace CrideLLMApi.VectorDB.ChromaDB;

public sealed record ChromaOptions
{
    public required Uri EndPoint { get; init; }

    public string Tenant { get; init; } = "default_tenant";

    public string Database { get; init; } = "default_database";

    public string? ApiKey { get; init; }
}