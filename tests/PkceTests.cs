using Bezalu.NinjaOne.MCP.Auth.OAuth;
using Xunit;

namespace Bezalu.NinjaOne.MCP.Tests;

public class PkceTests
{
    [Fact]
    public void Verify_S256_MatchesComputedChallenge()
    {
        var verifier = Pkce.NewToken(32);
        var challenge = Pkce.ComputeS256Challenge(verifier);

        Assert.True(Pkce.Verify(verifier, challenge, "S256"));
    }

    [Fact]
    public void Verify_S256_FailsForWrongVerifier()
    {
        var challenge = Pkce.ComputeS256Challenge(Pkce.NewToken(32));

        Assert.False(Pkce.Verify(Pkce.NewToken(32), challenge, "S256"));
    }

    [Fact]
    public void Verify_Plain_IsRejected()
    {
        var verifier = Pkce.NewToken(32);

        Assert.False(Pkce.Verify(verifier, verifier, "plain"));
    }

    [Fact]
    public void Verify_UnknownMethod_IsRejected()
    {
        var verifier = Pkce.NewToken(32);
        var challenge = Pkce.ComputeS256Challenge(verifier);

        Assert.False(Pkce.Verify(verifier, challenge, "something-else"));
    }

    [Theory]
    [InlineData("", "challenge", "S256")]
    [InlineData("verifier", "", "S256")]
    public void Verify_FailsForEmptyInputs(string verifier, string challenge, string method)
    {
        Assert.False(Pkce.Verify(verifier, challenge, method));
    }

    [Fact]
    public void Verify_ReturnsFalseForLengthMismatchedChallenge()
    {
        var verifier = Pkce.NewToken(32);

        // A malformed challenge whose length differs from the computed S256 challenge must return
        // false, not throw: FixedTimeEquals returns false (rather than throwing) on length mismatch.
        Assert.False(Pkce.Verify(verifier, "short", "S256"));
    }

    [Fact]
    public void NewToken_ProducesUrlSafeUniqueValues()
    {
        var a = Pkce.NewToken(32);
        var b = Pkce.NewToken(32);

        Assert.NotEqual(a, b);
        Assert.DoesNotContain('+', a);
        Assert.DoesNotContain('/', a);
        Assert.DoesNotContain('=', a);
    }
}
