/*
 * ThreadPilot - HTTP downloads for update assets.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class HttpUpdateDownloadClient : IUpdateDownloadClient
    {
        private readonly HttpClient httpClient;

        public HttpUpdateDownloadClient(HttpClient httpClient)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task DownloadFileAsync(Uri uri, string destinationPath, CancellationToken cancellationToken = default)
        {
            using var response = await this.httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var destination = File.Create(destinationPath);
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        public async Task<string?> TryDownloadStringAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            try
            {
                return await this.httpClient.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }
    }
}
