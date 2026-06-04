using System.Security.Cryptography;
using System.Text;

namespace Bezalu.NinjaOne.MCP.Auth.OAuth;

/// <summary>
/// PKCE (RFC 7636) utilities and secure random identifier/token generation.
/// </summary>
internal static class Pkce
{
    /// <summary>The only PKCE challenge method this server supports (advertised in metadata).</summary>
    public const string S256Method = "S256";

    /// <summary>
    /// Verifies a PKCE <paramref name="codeVerifier"/> against a previously supplied
    /// <paramref name="codeChallenge"/>. Only the <c>S256</c> <paramref name="method"/> is accepted;
    /// any other value is rejected to stay consistent with advertised server metadata.
    /// </summary>
    public static bool Verify(string codeVerifier, string codeChallenge, string method)
    {
        if (string.IsNullOrEmpty(codeVerifier) || string.IsNullOrEmpty(codeChallenge))
            return false;

        if (!IsSupportedMethod(method))
            return false;

        var computed = ComputeS256Challenge(codeVerifier);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(codeChallenge));
    }

    /// <summary>Returns true only for the supported S256 challenge method.</summary>
    public static bool IsSupportedMethod(string? method) =>
        string.Equals(method, S256Method, StringComparison.Ordinal);

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
