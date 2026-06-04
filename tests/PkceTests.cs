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
    public void Verify_Plain_MatchesIdenticalValue()
    {
        var verifier = Pkce.NewToken(32);

        Assert.True(Pkce.Verify(verifier, verifier, "plain"));
    }

    [Fact]
    public void Verify_DefaultsToS256_WhenMethodUnknown()
    {
        var verifier = Pkce.NewToken(32);
        var challenge = Pkce.ComputeS256Challenge(verifier);

        Assert.True(Pkce.Verify(verifier, challenge, "something-else"));
    }

    [Theory]
    [InlineData("", "challenge", "S256")]
    [InlineData("verifier", "", "S256")]
    public void Verify_FailsForEmptyInputs(string verifier, string challenge, string method)
    {
        Assert.False(Pkce.Verify(verifier, challenge, method));
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
