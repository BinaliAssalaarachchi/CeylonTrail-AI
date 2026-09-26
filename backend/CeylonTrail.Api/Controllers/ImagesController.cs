using Microsoft.AspNetCore.Mvc;

namespace CeylonTrail.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ImagesController(IHttpClientFactory httpClientFactory, ILogger<ImagesController> logger) : ControllerBase
{
    [HttpGet("proxy")]
    [HttpHead("proxy")]
    public async Task<IActionResult> Proxy([FromQuery] string? url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return BadRequest("A valid HTTP or HTTPS image URL is required.");
        }

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Accept.ParseAdd("image/webp,image/apng,image/*,*/*;q=0.8");

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to proxy image {Url}: status {StatusCode}", url, response.StatusCode);
                return StatusCode((int)response.StatusCode);
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            Response.Headers.CacheControl = "public, max-age=86400";
            return File(bytes, contentType);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error proxying image {Url}", url);
            return StatusCode(StatusCodes.Status502BadGateway, "Unable to fetch image.");
        }
    }
}
