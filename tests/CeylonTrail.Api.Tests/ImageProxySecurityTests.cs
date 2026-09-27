using System.Net;
using System.Net.Http.Headers;
using CeylonTrail.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class ImageProxySecurityTests
{
    [Theory]
    [InlineData("not-a-url")]
    [InlineData("/images/photo.jpg")]
    [InlineData("ftp://example.com/photo.jpg")]
    [InlineData("http://localhost/photo.jpg")]
    [InlineData("http://images.localhost/photo.jpg")]
    [InlineData("http://127.0.0.1/photo.jpg")]
    [InlineData("http://0.0.0.0/photo.jpg")]
    [InlineData("http://10.0.0.1/photo.jpg")]
    [InlineData("http://172.16.0.1/photo.jpg")]
    [InlineData("http://192.168.1.1/photo.jpg")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[::1]/photo.jpg")]
    [InlineData("http://[::]/photo.jpg")]
    [InlineData("http://[fe80::1]/photo.jpg")]
    [InlineData("http://[fd00::1]/photo.jpg")]
    [InlineData("http://[::ffff:127.0.0.1]/photo.jpg")]
    [InlineData("http://user:password@example.com/photo.jpg")]
    public async Task Proxy_RejectsUnsafeOrMalformedUrls(string url)
    {
        var handler = new RecordingHandler(_ => throw new Xunit.Sdk.XunitException("No outbound request expected."));
        var controller = CreateController(handler, new FakeDnsResolver());

        var result = await controller.Proxy(url, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData("http://public.example/photo.jpg")]
    [InlineData("https://public.example/photo.jpg")]
    public async Task Proxy_AllowsPublicHttpAndHttpsImageUrls(string url)
    {
        var handler = new RecordingHandler(_ => ImageResponse("image/png", new byte[] { 1, 2, 3 }));
        var dns = new FakeDnsResolver(("public.example", new[] { IPAddress.Parse("93.184.216.34") }));
        var controller = CreateController(handler, dns);

        var result = await controller.Proxy(url, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, file.FileContents);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Proxy_RejectsHostnameThatResolvesToPrivateAddress()
    {
        var handler = new RecordingHandler(_ => throw new Xunit.Sdk.XunitException("No outbound request expected."));
        var dns = new FakeDnsResolver(("attacker.example", new[] { IPAddress.Parse("192.168.1.10") }));
        var controller = CreateController(handler, dns);

        var result = await controller.Proxy("https://attacker.example/photo.jpg", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Proxy_RejectsRedirectToPrivateAddress()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("http://internal.example/photo.jpg") }
        });
        var dns = new FakeDnsResolver(
            ("public.example", new[] { IPAddress.Parse("93.184.216.34") }),
            ("internal.example", new[] { IPAddress.Parse("10.0.0.2") }));
        var controller = CreateController(handler, dns);

        var result = await controller.Proxy("https://public.example/photo.jpg", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Proxy_RejectsNonImageContent()
    {
        var handler = new RecordingHandler(_ => ImageResponse("text/html", new byte[] { 1 }));
        var controller = CreateController(handler, new FakeDnsResolver(
            ("public.example", new[] { IPAddress.Parse("93.184.216.34") })));

        var result = await controller.Proxy("https://public.example/page", CancellationToken.None);

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task Proxy_RejectsOversizedResponse()
    {
        var response = ImageResponse("image/jpeg", new byte[] { 1 });
        response.Content.Headers.ContentLength = 10 * 1024 * 1024 + 1;
        var handler = new RecordingHandler(_ => response);
        var controller = CreateController(handler, new FakeDnsResolver(
            ("public.example", new[] { IPAddress.Parse("93.184.216.34") })));

        var result = await controller.Proxy("https://public.example/large.jpg", CancellationToken.None);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    private static ImagesController CreateController(HttpMessageHandler handler, IImageProxyDnsResolver dns) =>
        new(new SingleClientFactory(new HttpClient(handler)), dns, NullLogger<ImagesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static HttpResponseMessage ImageResponse(string contentType, byte[] bytes)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return response;
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class FakeDnsResolver(params (string Host, IPAddress[] Addresses)[] entries) : IImageProxyDnsResolver
    {
        private readonly IReadOnlyDictionary<string, IPAddress[]> addresses =
            entries.ToDictionary(entry => entry.Host, entry => entry.Addresses, StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyCollection<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<IPAddress>>(
                addresses.TryGetValue(host, out var result) ? result : Array.Empty<IPAddress>());
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responder(request));
        }
    }
}
