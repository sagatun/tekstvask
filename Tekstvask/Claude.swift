import Foundation

enum ClaudeError: LocalizedError, Equatable {
    case invalidAPIKey
    case refusal
    case truncated
    case emptyResponse
    case api(status: Int, message: String)

    var errorDescription: String? {
        switch self {
        case .invalidAPIKey: String(localized: "The API key was rejected. Check it in Settings.")
        case .refusal: String(localized: "Claude declined to process this text.")
        case .truncated: String(localized: "The text is too long. Select less text and try again.")
        case .emptyResponse: String(localized: "Claude returned no text.")
        case let .api(status, message): String(localized: "Anthropic API error (\(status)): \(message)")
        }
    }
}

/// Minimal Messages API client. There is no official Swift SDK, so this is raw HTTP.
enum Claude {
    static let defaultModel = "claude-haiku-5-5"
    /// The models offered in Advanced settings, fastest first.
    static let models = [defaultModel, "claude-sonnet-5-5", "claude-opus-5-5"]

    struct Body: Encodable {
        struct Message: Encodable { let role: String, content: String }
        struct Thinking: Encodable { let type: String }
        struct OutputConfig: Encodable { let effort: String }

        let model: String
        // ponytail: non-streaming with a 16k cap; very long selections fail as .truncated. Switch to SSE if that matters.
        var maxTokens = 16_000
        let thinking: Thinking?
        // Low effort keeps short rewrites fast. None of these models accept sampling params.
        var outputConfig = OutputConfig(effort: "low")
        let fallbacks: String?
        let system: String
        let messages: [Message]

        init(model: String, system: String, text: String) {
            self.model = model
            // The least thinking each model allows: Sonnet 5.5 rejects "disabled", and Opus 5.5 always thinks.
            thinking = switch model {
            case "claude-haiku-5-5": Thinking(type: "disabled")
            case "claude-sonnet-5-5": Thinking(type: "between_tools")
            default: nil
            }
            // Re-runs a declined request on a model Anthropic picks. Haiku has no server-side fallback.
            fallbacks = model == "claude-haiku-5-5" ? nil : "default"
            self.system = system
            messages = [Message(role: "user", content: Prompts.wrap(text))]
        }
    }

    static let fallbackBeta = "server-side-fallback-2026-07-01"

    private struct Response: Decodable {
        struct Block: Decodable { let type: String, text: String? }
        let content: [Block]
        let stopReason: String?
    }

    private struct ErrorResponse: Decodable {
        struct Detail: Decodable { let message: String }
        let error: Detail
    }

    static func request(apiKey: String, model: String, system: String, text: String) throws -> URLRequest {
        var request = URLRequest(url: URL(string: "https://api.anthropic.com/v1/messages")!, timeoutInterval: 120)
        request.httpMethod = "POST"
        request.setValue(apiKey, forHTTPHeaderField: "x-api-key")
        request.setValue("2023-06-01", forHTTPHeaderField: "anthropic-version")
        request.setValue("application/json", forHTTPHeaderField: "content-type")
        let body = Body(model: model, system: system, text: text)
        if body.fallbacks != nil { request.setValue(fallbackBeta, forHTTPHeaderField: "anthropic-beta") }
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        request.httpBody = try encoder.encode(body)
        return request
    }

    static func parse(_ data: Data, status: Int) throws -> String {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        guard status == 200 else {
            if status == 401 { throw ClaudeError.invalidAPIKey }
            let message = (try? decoder.decode(ErrorResponse.self, from: data))?.error.message
            throw ClaudeError.api(status: status, message: message ?? String(decoding: data, as: UTF8.self))
        }
        let response = try decoder.decode(Response.self, from: data)
        switch response.stopReason {
        case "refusal": throw ClaudeError.refusal
        case "max_tokens": throw ClaudeError.truncated
        default: break
        }
        // Read blocks by type: a response may start with thinking blocks.
        let text = response.content.filter { $0.type == "text" }.compactMap(\.text).joined()
        guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw ClaudeError.emptyResponse }
        return text
    }

    static func complete(apiKey: String, model: String, system: String, text: String) async throws -> String {
        let (data, response) = try await URLSession.shared.data(for: request(apiKey: apiKey, model: model, system: system, text: text))
        return try parse(data, status: (response as? HTTPURLResponse)?.statusCode ?? 0)
    }
}
