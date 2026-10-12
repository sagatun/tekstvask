using System.Net;
using System.Text;
using System.Text.Json;

namespace Tekstvask.Tests;

/// <summary>Answers every request with one canned response and remembers what was sent.</summary>
internal sealed class FakeApi(HttpStatusCode status, string body) : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }
    public string? RequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Request = request;
        RequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(token);
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}

/// <summary>Never answers, like an API that takes too long.</summary>
internal sealed class SlowApi : HttpMessageHandler
{
    public int Attempts;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Interlocked.Increment(ref Attempts);
        await Task.Delay(Timeout.Infinite, token);
        throw new InvalidOperationException("unreachable");
    }
}

public class ClaudeTests
{
    private static string Message(string content, string stopReason) =>
        $$"""
        {"id":"msg_1","type":"message","role":"assistant","model":"claude-haiku-5-5","content":{{content}},
         "stop_reason":"{{stopReason}}","stop_sequence":null,"usage":{"input_tokens":1,"output_tokens":1} }
        """;

    private static async Task<string> Complete(FakeApi api, string model = Claude.DefaultModel) =>
        await Claude.Complete("k", model, "S", "hello", new HttpClient(api));

    private static FakeApi Ok() => new(HttpStatusCode.OK, Message("""[{"type":"text","text":"Hello"}]""", "end_turn"));

    [Fact]
    public async Task ReadsTextBlockAfterThinkingBlock()
    {
        var api = new FakeApi(HttpStatusCode.OK,
            Message("""[{"type":"thinking","thinking":"","signature":"x"},{"type":"text","text":"Hei"}]""", "end_turn"));
        Assert.Equal("Hei", await Complete(api));
    }

    [Fact]
    public async Task RefusalIsAnError()
    {
        var e = await Assert.ThrowsAsync<ClaudeException>(() => Complete(new FakeApi(HttpStatusCode.OK, Message("[]", "refusal"))));
        Assert.Equal(ClaudeError.Refusal, e.Error);
    }

    [Fact]
    public async Task TruncatedReplyIsNeverReturned()
    {
        var api = new FakeApi(HttpStatusCode.OK, Message("""[{"type":"text","text":"Half a"}]""", "max_tokens"));
        var e = await Assert.ThrowsAsync<ClaudeException>(() => Complete(api));
        Assert.Equal(ClaudeError.Truncated, e.Error);
    }

    [Fact]
    public async Task EmptyReplyIsAnError()
    {
        var api = new FakeApi(HttpStatusCode.OK, Message("""[{"type":"text","text":"  "}]""", "end_turn"));
        var e = await Assert.ThrowsAsync<ClaudeException>(() => Complete(api));
        Assert.Equal(ClaudeError.EmptyResponse, e.Error);
    }

    [Fact]
    public async Task UnauthorizedMeansBadKey()
    {
        var api = new FakeApi(HttpStatusCode.Unauthorized,
            """{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""");
        var e = await Assert.ThrowsAsync<ClaudeException>(() => Complete(api));
        Assert.Equal(ClaudeError.InvalidApiKey, e.Error);
    }

    [Fact]
    public async Task ApiErrorCarriesStatusAndMessage()
    {
        // 400 rather than 529: the SDK retries overloaded errors with backoff.
        var api = new FakeApi(HttpStatusCode.BadRequest,
            """{"type":"error","error":{"type":"invalid_request_error","message":"Bad model"}}""");
        var e = await Assert.ThrowsAsync<ClaudeException>(() => Complete(api));
        Assert.Equal(ClaudeError.Api, e.Error);
        Assert.Equal("Anthropic API error (400): Bad model", e.Message);
    }

    [Fact]
    public async Task SlowReplyTimesOutAfterOneRetry()
    {
        var api = new SlowApi();
        var e = await Assert.ThrowsAsync<ClaudeException>(() =>
            Claude.Complete("k", Claude.DefaultModel, "S", "hello", new HttpClient(api), TimeSpan.FromMilliseconds(200)));
        Assert.Equal(ClaudeError.Timeout, e.Error);
        Assert.Equal(2, api.Attempts);
    }

    [Fact]
    public async Task RequestFollowsHaikuContract()
    {
        var api = Ok();
        await Complete(api);

        // The beta endpoint may add a query string, so only the path is checked.
        Assert.Equal("https://api.anthropic.com/v1/messages", api.Request!.RequestUri!.GetLeftPart(UriPartial.Path));
        Assert.Equal("k", api.Request.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", api.Request.Headers.GetValues("anthropic-version").Single());
        var body = JsonDocument.Parse(api.RequestBody!).RootElement;
        Assert.Equal("claude-haiku-5-5", body.GetProperty("model").GetString());
        Assert.Equal(16_000, body.GetProperty("max_tokens").GetInt32());
        Assert.Equal("disabled", body.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal("low", body.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.Equal("S", body.GetProperty("system").GetString());
        // Haiku 5.5 returns 400 on sampling parameters, and has no server-side fallback.
        Assert.False(body.TryGetProperty("temperature", out _) || body.TryGetProperty("top_p", out _) || body.TryGetProperty("top_k", out _));
        Assert.False(body.TryGetProperty("fallbacks", out _) || api.Request.Headers.Contains("anthropic-beta"));
        var message = Assert.Single(body.GetProperty("messages").EnumerateArray());
        Assert.Equal("user", message.GetProperty("role").GetString());
        Assert.Equal("<text>\nhello\n</text>", message.GetProperty("content").GetString());
    }

    [Fact]
    public async Task SonnetThinksOnlyBetweenTools()
    {
        // "disabled" is a 400 on Sonnet 5.5.
        var api = Ok();
        await Complete(api, "claude-sonnet-5-5");
        var body = JsonDocument.Parse(api.RequestBody!).RootElement;
        Assert.Equal("""{"type":"between_tools"}""", body.GetProperty("thinking").GetRawText());
        Assert.Equal("low", body.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.Equal("default", body.GetProperty("fallbacks").GetString());
        Assert.Equal("server-side-fallback-2026-07-01", api.Request!.Headers.GetValues("anthropic-beta").Single());
    }

    [Fact]
    public async Task OpusSendsNoThinkingField()
    {
        // Opus 5.5 always thinks; both "disabled" and a budget are 400s.
        var api = Ok();
        await Complete(api, "claude-opus-5-5");
        var body = JsonDocument.Parse(api.RequestBody!).RootElement;
        Assert.False(body.TryGetProperty("thinking", out _));
        Assert.Equal("low", body.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.Equal("default", body.GetProperty("fallbacks").GetString());
        Assert.Equal("server-side-fallback-2026-07-01", api.Request!.Headers.GetValues("anthropic-beta").Single());
    }
}
