import Foundation
import Security

/// The Anthropic API key, stored in the login keychain.
enum Keychain {
    private static var query: [String: Any] {
        [kSecClass as String: kSecClassGenericPassword,
         kSecAttrService as String: "Tekstvask",
         kSecAttrAccount as String: "anthropic-api-key"]
    }

    static var apiKey: String? {
        get {
            var lookup = query
            lookup[kSecReturnData as String] = true
            var result: CFTypeRef?
            guard SecItemCopyMatching(lookup as CFDictionary, &result) == errSecSuccess,
                  let data = result as? Data, let key = String(data: data, encoding: .utf8), !key.isEmpty
            else { return nil }
            return key
        }
        set {
            SecItemDelete(query as CFDictionary)
            guard let newValue, !newValue.isEmpty else { return }
            var item = query
            item[kSecValueData as String] = Data(newValue.utf8)
            SecItemAdd(item as CFDictionary, nil)
        }
    }
}
