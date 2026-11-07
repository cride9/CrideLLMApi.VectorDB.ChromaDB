namespace CrideLLMApi.VectorDB.ChromaDB;

public sealed record ChromaOptions
{
    public Uri EndPoint { get; init; } = new("http://127.0.0.1:8000");

    public string Tenant { get; init; } = "default_tenant";

    public string Database { get; init; } = "default_database";

    public string? ApiKey { get; init; }

    public bool AutoStart { get; init; } = true;

    /// <summary>Reject an already running server whose data directory cannot be verified.</summary>
    public bool RequireManagedServer { get; init; }

    /// <summary>Use a preinstalled Chroma CLI, for example inside an offline container.</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>Allow creating a virtual environment and downloading packages at startup.</summary>
    public bool InstallOnStartup { get; init; } = true;

    /// <summary>Forward the Chroma process output to the application console.</summary>
    public bool ForwardServerLogs { get; init; } = true;

    public string RuntimePath { get; init; }
        = Path.Combine(Environment.CurrentDirectory, ".cride", "chromadb");

    public string DataPath { get; init; }
        = Path.Combine(Environment.CurrentDirectory, ".cride", "chromadb", "data");
}
