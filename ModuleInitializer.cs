using System.Runtime.CompilerServices;

namespace CrideLLMApi.VectorDB.ChromaDB;

internal static class ModuleInitializer
{
    /// <summary>
    /// Initializes the module by registering the ChromaDBProvider with the VectorDatabaseRegistry.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        VectorDatabaseRegistry.Register(
            new ChromaDBProvider());
    }
}