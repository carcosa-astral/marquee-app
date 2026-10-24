using System.Net;
using System.Text;

namespace Marquee.Metadata.Tests;

/// <summary>Answers HTTP requests from a list of canned responses, and records what was asked.</summary>
internal sealed class FakeHttp : HttpMessageHandler
{
    private readonly List<(Func<HttpRequestMessage, bool> Match, Func<HttpResponseMessage> Respond)> _routes = [];

    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string> Bodies { get; } = [];

    public FakeHttp On(string urlContains, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes.Add((r => r.RequestUri!.ToString().Contains(urlContains), () => Json(json, status)));
        return this;
    }

    public FakeHttp OnBody(string bodyContains, string json)
    {
        _routes.Add((r => Bodies[^1].Contains(bodyContains), () => Json(json, HttpStatusCode.OK)));
        return this;
    }

    public FakeHttp OnBytes(string urlContains, byte[] bytes)
    {
        _routes.Add((r => r.RequestUri!.ToString().Contains(urlContains), () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }));
        return this;
    }

    public HttpClient Client() => new(this);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
        foreach (var (match, respond) in _routes)
        {
            if (match(request))
                return respond();
        }
        return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
