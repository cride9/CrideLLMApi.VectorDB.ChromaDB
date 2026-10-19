using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CrideLLMApi.VectorDB.ChromaDB;

internal sealed class ChromaTransport : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ChromaOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void Dispose() => _httpClient.Dispose();

    public ChromaTransport(ChromaOptions options)
    {
        _options = options;

        _httpClient = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
        {
            BaseAddress = options.EndPoint
        };

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    options.ApiKey);
        }
    }

    /// <summary>
    /// Sends a GET request to the specified path and deserializes the response into the specified type.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="path">The path to send the request to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    public async Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken = default)
    {
        using var response =
            await _httpClient.GetAsync(
                path,
                cancellationToken);

        return await DeserializeResponseAsync<TResponse>(
            response,
            cancellationToken);
    }

    /// <summary>
    /// Sends a POST request to the specified path with the provided request body and deserializes the response into the specified type.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request body.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="path">The path to send the request to.</param>
    /// <param name="request">The request body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    public async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response =
            await _httpClient.PostAsJsonAsync(
                path,
                request,
                cancellationToken);

        return await DeserializeResponseAsync<TResponse>(
            response,
            cancellationToken);
    }

    /// <summary>
    /// Sends a POST request to the specified path with the provided request body. Throws an exception if the response indicates failure.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request body.</typeparam>
    /// <param name="path">The path to send the request to.</param>
    /// <param name="request">The request body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="HttpRequestException"></exception>
    public async Task PostAsync<TRequest>(string path, TRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response =
            await _httpClient.PostAsJsonAsync(
                path,
                request,
                cancellationToken);

        if (response.IsSuccessStatusCode)
            return;

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        throw new HttpRequestException(
            $"ChromaDB request failed: " +
            $"{(int)response.StatusCode} {response.ReasonPhrase}\n" +
            body);
    }

    /// <summary>
    /// Sends a DELETE request to the specified path.
    /// </summary>
    /// <param name="path">The path to send the request to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="HttpRequestException"></exception>
    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        using var response =
            await _httpClient.DeleteAsync(
                path,
                cancellationToken);

        if (response.IsSuccessStatusCode)
            return;

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        throw new HttpRequestException(
            $"ChromaDB request failed: " +
            $"{(int)response.StatusCode} {response.ReasonPhrase}\n" +
            body);
    }

    /// <summary>
    /// Deserializes the HTTP response into the specified type. Throws an exception if the response indicates failure or if deserialization fails.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="response">The HTTP response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    /// <exception cref="HttpRequestException"></exception>
    /// <exception cref="JsonException"></exception>
    private static async Task<TResponse> DeserializeResponseAsync<TResponse>(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            throw new HttpRequestException(
                $"ChromaDB request failed: " +
                $"{(int)response.StatusCode} {response.ReasonPhrase}\n" +
                body);
        }

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        var result =
            await JsonSerializer.DeserializeAsync<TResponse>(
                stream,
                JsonOptions,
                cancellationToken);

        return result
            ?? throw new JsonException(
                "ChromaDB returned an empty response.");
    }
}
