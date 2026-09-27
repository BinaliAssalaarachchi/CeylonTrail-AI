using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc;

namespace CeylonTrail.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ImagesController(
    IHttpClientFactory httpClientFactory,
    IImageProxyDnsResolver dnsResolver,
    ILogger<ImagesController> logger) : ControllerBase
{
    private const int MaxRedirects = 3;
    private const long MaxImageBytes = 10 * 1024 * 1024;

    [HttpGet("proxy")]
    [HttpHead("proxy")]
    public async Task<IActionResult> Proxy([FromQuery] string? url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !IsAllowedScheme(uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return BadRequest("A valid HTTP or HTTPS image URL is required.");
        }

        try
        {
            if (!await IsSafeTargetAsync(uri, cancellationToken))
            {
                return BadRequest("The requested image URL is not allowed.");
            }

            var client = httpClientFactory.CreateClient("ImageProxy");
            client.Timeout = TimeSpan.FromSeconds(10);

            for (var redirectCount = 0; redirectCount <= MaxRedirects; redirectCount++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd("CeylonTrail-ImageProxy/1.0");
                request.Headers.Accept.ParseAdd("image/*");

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (IsRedirect(response.StatusCode))
                {
                    if (redirectCount == MaxRedirects ||
                        response.Headers.Location is null ||
                        !Uri.TryCreate(uri, response.Headers.Location, out var redirectUri) ||
                        !IsAllowedScheme(redirectUri) ||
                        !string.IsNullOrEmpty(redirectUri.UserInfo) ||
                        !await IsSafeTargetAsync(redirectUri, cancellationToken))
                    {
                        logger.LogWarning("Rejected unsafe image redirect from host {Host}.", uri.Host);
                        return BadRequest("The requested image URL is not allowed.");
                    }

                    uri = redirectUri;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Failed to proxy image from host {Host}: status {StatusCode}", uri.Host, response.StatusCode);
                    return StatusCode((int)response.StatusCode);
                }

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (string.IsNullOrWhiteSpace(contentType) ||
                    !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Rejected non-image response from host {Host}.", uri.Host);
                    return StatusCode(StatusCodes.Status415UnsupportedMediaType);
                }

                if (response.Content.Headers.ContentLength is > MaxImageBytes)
                {
                    logger.LogWarning("Rejected oversized image response from host {Host}.", uri.Host);
                    return StatusCode(StatusCodes.Status413PayloadTooLarge);
                }

                var bytes = await ReadBoundedAsync(response.Content, cancellationToken);
                Response.Headers.CacheControl = "public, max-age=86400";
                return File(bytes, contentType);
            }

            return StatusCode(StatusCodes.Status502BadGateway);
        }
        catch (ImageProxyResponseTooLargeException)
        {
            logger.LogWarning("Rejected oversized image response from host {Host}.", uri.Host);
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Image proxy request timed out for host {Host}.", uri.Host);
            return StatusCode(StatusCodes.Status502BadGateway, "Unable to fetch image.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Image proxy failed for host {Host}: {FailureType}", uri.Host, ex.GetType().Name);
            return StatusCode(StatusCodes.Status502BadGateway, "Unable to fetch image.");
        }
    }

    private async Task<bool> IsSafeTargetAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IPAddress.TryParse(uri.Host, out var address))
        {
            return !IsProhibitedAddress(address);
        }

        IReadOnlyCollection<IPAddress> addresses;
        try
        {
            using var dnsTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            dnsTimeout.CancelAfter(TimeSpan.FromSeconds(2));
            addresses = await dnsResolver.ResolveAsync(uri.Host, dnsTimeout.Token);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            logger.LogWarning("Image proxy DNS resolution failed for host {Host}.", uri.Host);
            return false;
        }

        return addresses.Count > 0 && addresses.All(address => !IsProhibitedAddress(address));
    }

    private static bool IsAllowedScheme(Uri uri) =>
        uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or
            HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static bool IsProhibitedAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || IPAddress.Any.Equals(address) || IPAddress.IPv6Any.Equals(address))
        {
            return true;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                bytes[0] >= 224;
        }

        var ipv6 = address.GetAddressBytes();
        return (ipv6[0] & 0xFE) == 0xFC ||
            (ipv6[0] == 0xFE && (ipv6[1] & 0xC0) == 0x80) ||
            address.IsIPv6Multicast;
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        var total = 0L;
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxImageBytes)
            {
                throw new ImageProxyResponseTooLargeException();
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }
}

public interface IImageProxyDnsResolver
{
    Task<IReadOnlyCollection<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken);
}

public sealed class SystemImageProxyDnsResolver : IImageProxyDnsResolver
{
    public async Task<IReadOnlyCollection<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        await Dns.GetHostAddressesAsync(host, cancellationToken);
}

public sealed class ImageProxyResponseTooLargeException : Exception;
