import Foundation
import Testing

private func parse(_ json: String, status: Int = 200) throws -> String {
    try Claude.parse(Data(json.utf8), status: status)
}

@Test func readsTextBlockAfterThinkingBlock() throws {
    let json = #"{"content":[{"type":"thinking","thinking":"","signature":"x"},{"type":"text","text":"Hei"}],"stop_reason":"end_turn"}"#
    #expect(try parse(json) == "Hei")
}

@Test func refusalIsAnError() {
    #expect(throws: ClaudeError.refusal) { try parse(#"{"content":[],"stop_reason":"refusal"}"#) }
}

@Test func truncatedReplyIsNeverReturned() {
    #expect(throws: ClaudeError.truncated) { try parse(#"{"content":[{"type":"text","text":"Half a"}],"stop_reason":"max_tokens"}"#) }
}

@Test func emptyReplyIsAnError() {
    #expect(throws: ClaudeError.emptyResponse) { try parse(#"{"content":[{"type":"text","text":"  "}],"stop_reason":"end_turn"}"#) }
}

@Test func unauthorizedMeansBadKey() {
    #expect(throws: ClaudeError.invalidAPIKey) { try parse(#"{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}"#, status: 401) }
}

@Test func apiErrorCarriesMessage() {
    let json = #"{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}"#
    #expect(throws: ClaudeError.api(status: 529, message: "Overloaded")) { try parse(json, status: 529) }
}

private func body(_ request: URLRequest) throws -> [String: Any] {
    try #require(JSONSerialization.jsonObject(with: request.httpBody!) as? [String: Any])
}

@Test func requestFollowsHaikuContract() throws {
    let request = try Claude.request(apiKey: "k", model: Claude.defaultModel, system: "S", text: "hello")
    let body = try body(request)
    #expect(request.value(forHTTPHeaderField: "x-api-key") == "k")
    #expect(request.value(forHTTPHeaderField: "anthropic-version") == "2023-06-01")
    #expect(body["model"] as? String == "claude-haiku-5-5")
    #expect(body["max_tokens"] as? Int == 16_000)
    #expect((body["thinking"] as? [String: String])?["type"] == "disabled")
    #expect((body["output_config"] as? [String: String])?["effort"] == "low")
    #expect(body["system"] as? String == "S")
    // Haiku 5.5 returns 400 on sampling parameters, and has no server-side fallback.
    #expect(body["temperature"] == nil && body["top_p"] == nil && body["top_k"] == nil)
    #expect(body["fallbacks"] == nil && request.value(forHTTPHeaderField: "anthropic-beta") == nil)
    let messages = try #require(body["messages"] as? [[String: String]])
    #expect(messages == [["role": "user", "content": "<text>\nhello\n</text>"]])
}

@Test func sonnetThinksOnlyBetweenTools() throws {
    // "disabled" is a 400 on Sonnet 5.5.
    let request = try Claude.request(apiKey: "k", model: "claude-sonnet-5-5", system: "S", text: "hello")
    let body = try body(request)
    #expect((body["thinking"] as? [String: String]) == ["type": "between_tools"])
    #expect((body["output_config"] as? [String: String])?["effort"] == "low")
    #expect(body["fallbacks"] as? String == "default")
    #expect(request.value(forHTTPHeaderField: "anthropic-beta") == "server-side-fallback-2026-07-01")
}

@Test func opusSendsNoThinkingField() throws {
    // Opus 5.5 always thinks; both "disabled" and a budget are 400s.
    let request = try Claude.request(apiKey: "k", model: "claude-opus-5-5", system: "S", text: "hello")
    let body = try body(request)
    #expect(body["thinking"] == nil)
    #expect((body["output_config"] as? [String: String])?["effort"] == "low")
    #expect(body["fallbacks"] as? String == "default")
    #expect(request.value(forHTTPHeaderField: "anthropic-beta") == "server-side-fallback-2026-07-01")
}

@Test func onlyOfferedModelsAreUsed() {
    #expect(Preferences.model(stored: "claude-opus-5-5") == "claude-opus-5-5")
    #expect(Preferences.model(stored: "claude-sonnet-5") == Claude.defaultModel)
    #expect(Preferences.model(stored: nil) == Claude.defaultModel)
}

@Test func translatePromptNamesBothLanguagesInEnglish() {
    let prompt = Prompts.filling(Prompts.translate, target: "nb", second: "sv")
    #expect(prompt.contains("Translate it into Norwegian Bokmål. If it is already in Norwegian Bokmål, translate it into Swedish instead."))
    #expect(!prompt.contains("{"))
}

@Test func secondLanguageIsNeverTheTarget() {
    #expect(Preferences.secondLanguage(stored: "de", target: "en", system: "nb") == "de")
    // Falls back to the system language, then to English, or Norwegian when the target is English.
    #expect(Preferences.secondLanguage(stored: "en", target: "en", system: "nb") == "nb")
    #expect(Preferences.secondLanguage(stored: nil, target: "nb", system: "nb") == "en")
    #expect(Preferences.secondLanguage(stored: nil, target: "en", system: "en") == "nb")
    #expect(Preferences.secondLanguage(stored: nil, target: "en", system: "ja") == "nb")
    #expect(Preferences.secondLanguage(stored: nil, target: "en", system: "de") == "de")
}

@Test func keepsSelectionsOuterWhitespace() {
    #expect(Prompts.restoringOuterWhitespace(of: "  hei verden\n", in: "\nHei, verden!  ") == "  Hei, verden!\n")
    #expect(Prompts.restoringOuterWhitespace(of: "x", in: " y ") == "y")
}
