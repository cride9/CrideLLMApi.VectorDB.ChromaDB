namespace CrideLLMApi.VectorDB.ChromaDB;

internal sealed record PythonCommand(
    string Executable,
    string ArgumentsPrefix = "");