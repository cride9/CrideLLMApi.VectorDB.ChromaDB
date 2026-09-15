namespace CrideLLMApi.VectorDB.ChromaDB;

public sealed record ChromaOptions
{
    public Uri EndPoint { get; init; } = new("http://127.0.0.1:8000");

    public string Tenant { get; init; } = "default_tenant";

    public string Database { get; init; } = "default_database";

    public string? ApiKey { get; init; }

    public bool AutoStart { get; init; } = true;

    public string RuntimePath { get; init; }
        = Path.Combine(Environment.CurrentDirectory, ".cride", "chromadb");

    public string DataPath { get; init; }
        = Path.Combine(Environment.CurrentDirectory, ".cride", "chromadb", "data");
}