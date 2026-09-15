namespace CrideLLMApi.VectorDB.ChromaDB;

public static class ChromaDBExtension
{
    public static void Register()
    {
        VectorDatabaseRegistry.Register(
            new ChromaDBProvider());
    }
}