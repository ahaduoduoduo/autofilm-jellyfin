using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.AutoFilm;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// HTTP implementation of the AutoFilm OpenList client.
/// </summary>
public sealed class AutoFilmOpenListClient : IAutoFilmOpenListClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AutoFilmOptions _options;
    private readonly Uri? _baseUri;
    private readonly Uri? _publicBaseUri;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoFilmOpenListClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="options">AutoFilm configuration.</param>
    public AutoFilmOpenListClient(
        IHttpClientFactory httpClientFactory,
        AutoFilmOptions options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        if (Uri.TryCreate(options.OpenListUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
        {
            _baseUri = baseUri;
        }

        if (Uri.TryCreate(
                options.OpenListPublicUrl.TrimEnd('/') + "/",
                UriKind.Absolute,
                out var publicBaseUri))
        {
            _publicBaseUri = publicBaseUri;
        }
    }

    /// <inheritdoc />
    public async Task<AutoFilmOpenListObject?> GetObjectAsync(
        string path,
        CancellationToken cancellationToken)
    {
        return await GetObjectAsync(
            path,
            false,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AutoFilmOpenListObject?> GetObjectAsync(
        string path,
        bool refresh,
        CancellationToken cancellationToken)
    {
        var response = await PostAsync<AutoFilmOpenListObject>(
            "api/autofilm/objects/get",
            new { path, refresh },
            cancellationToken).ConfigureAwait(false);
        return response.Code == 404
            ? null
            : RequireData(response);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AutoFilmOpenListObject>> ListObjectsAsync(
        string path,
        bool refresh,
        CancellationToken cancellationToken)
    {
        var response = await PostAsync<AutoFilmOpenListListResponse>(
            "api/autofilm/objects/list",
            new { path, refresh },
            cancellationToken).ConfigureAwait(false);
        return RequireData(response).Objects;
    }

    /// <inheritdoc />
    public Uri GetDownloadUri(AutoFilmOpenListObject obj)
    {
        EnsureConfigured();
        return CreateDownloadUri(_publicBaseUri!, obj);
    }

    /// <inheritdoc />
    public Uri GetInternalDownloadUri(AutoFilmOpenListObject obj)
    {
        EnsureConfigured();
        return CreateDownloadUri(_baseUri!, obj);
    }

    private static Uri CreateDownloadUri(
        Uri baseUri,
        AutoFilmOpenListObject obj)
    {
        if (string.IsNullOrWhiteSpace(obj.DownloadPath))
        {
            throw new InvalidOperationException("OpenList object does not contain a download path.");
        }

        return new Uri(baseUri, obj.DownloadPath.TrimStart('/'));
    }

    /// <inheritdoc />
    public async Task UploadContentAsync(
        string remotePath,
        Stream content,
        long? contentLength,
        CancellationToken cancellationToken)
    {
        using var httpContent = new StreamContent(content);
        httpContent.Headers.ContentLength = contentLength;
        await UploadContentAsync(
            remotePath,
            httpContent,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task UploadContentAsync(
        string remotePath,
        HttpContent content,
        CancellationToken cancellationToken)
    {
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var request = CreateRequest(HttpMethod.Put, "api/autofilm/objects/put");
        request.Headers.Add("File-Path", Uri.EscapeDataString(remotePath));
        request.Headers.Add("As-Task", "false");
        request.Headers.Add("Overwrite", "false");
        request.Content = content;
        var client = _httpClientFactory.CreateClient();
        client.Timeout = Timeout.InfiniteTimeSpan;
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<OpenListEnvelope<JsonElement>>(
            SerializerOptions,
            cancellationToken).ConfigureAwait(false);
        if (envelope is null || envelope.Code != 200)
        {
            throw new InvalidOperationException(
                envelope?.Message ?? "OpenList upload returned an invalid response.");
        }
    }

    /// <inheritdoc />
    public async Task DeletePathAsync(
        string remotePath,
        CancellationToken cancellationToken)
    {
        var response = await PostAsync<JsonElement>(
            "api/autofilm/objects/delete",
            new { path = remotePath },
            cancellationToken).ConfigureAwait(false);
        if (response.Code is not 200 and not 404)
        {
            throw new InvalidOperationException(
                $"OpenList delete failed with code {response.Code}: {response.Message}");
        }
    }

    private async Task<OpenListEnvelope<T>> PostAsync<T>(
        string relativeUri,
        object body,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Post, relativeUri);
        request.Content = JsonContent.Create(body, options: SerializerOptions);
        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OpenListEnvelope<T>>(
            SerializerOptions,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("OpenList returned an empty response.");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri)
    {
        var request = new HttpRequestMessage(method, new Uri(_baseUri!, relativeUri));
        request.Headers.TryAddWithoutValidation("Authorization", _options.OpenListToken);
        return request;
    }

    private void EnsureConfigured()
    {
        if (!_options.IsOpenListConfigured || _baseUri is null || _publicBaseUri is null)
        {
            throw new InvalidOperationException(
                "AutoFilm OpenList internal URL, public URL, and token must be configured.");
        }
    }

    private static T RequireData<T>(OpenListEnvelope<T> response)
    {
        if (response.Code != 200 || response.Data is null)
        {
            throw new InvalidOperationException(
                $"OpenList request failed with code {response.Code}: {response.Message}");
        }

        return response.Data;
    }

    private sealed record AutoFilmOpenListListResponse
    {
        [JsonPropertyName("objects")]
        public IReadOnlyList<AutoFilmOpenListObject> Objects { get; init; } =
            Array.Empty<AutoFilmOpenListObject>();
    }

    private sealed record OpenListEnvelope<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; init; }

        [JsonPropertyName("message")]
        public string Message { get; init; } = string.Empty;

        [JsonPropertyName("data")]
        public T? Data { get; init; }
    }
}
