using System.Net.Http;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;

namespace Tekstvask;

public enum ClaudeError { InvalidApiKey, Refusal, Truncated, EmptyResponse, Timeout, Api }

public sealed class ClaudeException(ClaudeError error, string message) : Exception(message)
{
    public ClaudeError Error { get; } = error;

    public static ClaudeException Of(ClaudeError error) => new(error, error switch
    {
        ClaudeError.InvalidApiKey => Strings.T("The API key was rejected. Check it in Settings."),
        ClaudeError.Refusal => Strings.T("Claude declined to process this text."),
        ClaudeError.Truncated => Strings.T("The text is too long. Select less text and try again."),
        ClaudeError.Timeout => Strings.T("Claude took too long to answer. Try again, or select less text."),
        _ => Strings.T("Claude returned no text."),
    });

    public static ClaudeException Api(int status, string message) =>
        new(ClaudeError.Api, Strings.T("Anthropic API error ({0}): {1}", status, message));
}

/// <summary>Messages API calls through the official SDK, with the same request contract as Claude.swift.</summary>
public static class Claude
{
    public const string DefaultModel = "claude-haiku-5-5";
    /// <summary>The models offered in Advanced settings, fastest first.</summary>
    public static readonly string[] Models = [DefaultModel, "claude-sonnet-5-5", "claude-opus-5-5"];
    public const string FallbackBeta = "server-side-fallback-2026-07-01";

    // No timeout of its own (HttpClient's default is 100 s), so the SDK's Timeout below is the one that applies.
    private static readonly HttpClient SharedHttp = new() { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    public static MessageCreateParams Request(string model, string system, string text)
    {
        var request = new MessageCreateParams
        {
            Model = model,
            // Non-streaming with a 16k cap; very long selections fail as Truncated, like on macOS.
            MaxTokens = 16_000,
            // Low effort keeps short rewrites fast. None of these models accept sampling params.
            OutputConfig = new BetaOutputConfig { Effort = Effort.Low },
            System = system,
            Messages = [new() { Role = Role.User, Content = Prompts.Wrap(text) }],
        };
        // The least thinking each model allows: Sonnet 5.5 rejects "disabled", and Opus 5.5 always thinks.
        // Fallbacks re-run a declined request on a model Anthropic picks; Haiku has no server-side fallback.
        // Only set what's wanted: the SDK sends "fallbacks": null for a null Fallbacks.
        return model switch
        {
            DefaultModel => request with { Thinking = new BetaThinkingConfigDisabled() },
            "claude-sonnet-5-5" => request with { Thinking = new BetaThinkingConfigBetweenTools(), Fallbacks = new Default(), Betas = [FallbackBeta] },
            _ => request with { Fallbacks = new Default(), Betas = [FallbackBeta] },
        };
    }

    /// <summary>The reply text. A refused or cut-off reply is an error, so a selection is never replaced with half a text.</summary>
    public static string Text(BetaMessage response)
    {
        if (response.StopReason == BetaStopReason.Refusal) throw ClaudeException.Of(ClaudeError.Refusal);
        if (response.StopReason == BetaStopReason.MaxTokens) throw ClaudeException.Of(ClaudeError.Truncated);
        // Read blocks by type: a response may start with thinking blocks.
        string text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(b => b.Text));
        if (string.IsNullOrWhiteSpace(text)) throw ClaudeException.Of(ClaudeError.EmptyResponse);
        return text;
    }

    public static async Task<string> Complete(string apiKey, string model, string system, string text, HttpClient? http = null, TimeSpan? timeout = null)
    {
        // One retry covers a dropped connection or a busy API. The SDK also retries a request that timed out, and each
        // attempt is paid for, so not the default two.
        var client = new AnthropicClient
        {
            ApiKey = apiKey, HttpClient = http ?? SharedHttp, Timeout = timeout ?? TimeSpan.FromSeconds(120), MaxRetries = 1,
        };
        try
        {
            return Text(await client.Beta.Messages.Create(Request(model, system, text)));
        }
        catch (AnthropicUnauthorizedException)
        {
            throw ClaudeException.Of(ClaudeError.InvalidApiKey);
        }
        catch (AnthropicApiException e)
        {
            throw ClaudeException.Api((int)e.StatusCode, ErrorMessage(e.ResponseBody));
        }
        catch (TaskCanceledException e) when (e.InnerException is TimeoutException)
        {
            throw ClaudeException.Of(ClaudeError.Timeout);
        }
    }

    /// <summary>The `error.message` of an API error body, or the whole body if it isn't one.</summary>
    private static string ErrorMessage(string body)
    {
        try
        {
            return JsonDocument.Parse(body).RootElement.GetProperty("error").GetProperty("message").GetString() ?? body;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return body;
        }
    }
}
