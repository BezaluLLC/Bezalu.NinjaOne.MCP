using System.Security.Cryptography;
using System.Text;

namespace Bezalu.NinjaOne.MCP.Auth.OAuth;

/// <summary>
/// PKCE (RFC 7636) utilities and secure random identifier/token generation.
/// </summary>
internal static class Pkce
{
    /// <summary>
    /// Verifies a PKCE <paramref name="codeVerifier"/> against a previously supplied
    /// <paramref name="codeChallenge"/> using the given <paramref name="method"/> (S256 or plain).
    /// </summary>
    public static bool Verify(string codeVerifier, string codeChallenge, string method)
    {
        if (string.IsNullOrEmpty(codeVerifier) || string.IsNullOrEmpty(codeChallenge))
            return false;

        if (string.Equals(method, "plain", StringComparison.OrdinalIgnoreCase))
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(codeVerifier),
                Encoding.ASCII.GetBytes(codeChallenge));

        // Default to S256.
        var computed = ComputeS256Challenge(codeVerifier);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(codeChallenge));
    }

    /// <summary>Computes the S256 code challenge for a verifier: BASE64URL(SHA256(verifier)).</summary>
    public static string ComputeS256Challenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    /// <summary>Generates a cryptographically-random, URL-safe token of the given byte length.</summary>
    public static string NewToken(int byteLength = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
