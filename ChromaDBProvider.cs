using CrideLLMApi.Api;
using CrideLLMApi.DTO;

namespace CrideLLMApi.VectorDB.ChromaDB;

public sealed class ChromaDBProvider
    : IVectorDatabaseProvider
{
    public VectorDatabaseType Type =>
        VectorDatabaseType.CHROMADB;

    /// <summary>
    /// Creates a new instance of the ChromaDatabase class.
    /// </summary>
    /// <param name="api">The CrideApi instance.</param>
    /// <param name="providerInfo">The provider information.</param>
    /// <returns>The created IVectorDatabase instance.</returns>
    public IVectorDatabase Create(
        CrideApi api,
        ProviderInfo providerInfo)
    {
        return new ChromaDatabase(
            api,
            providerInfo);
    }
}