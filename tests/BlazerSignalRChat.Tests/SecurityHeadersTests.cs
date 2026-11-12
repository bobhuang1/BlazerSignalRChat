using BlazerSignalRChat.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazerSignalRChat.Tests;

public class SecurityHeadersTests
{
    [Fact]
    public async Task Response_carries_csp_and_hardening_headers()
    {
        var app = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        app.UseSecurityHeaders();
        var pipeline = app.Build();
        var ctx = new DefaultHttpContext();
        await pipeline(ctx);

        var h = ctx.Response.Headers;
        var nonce = (string)ctx.Items[SecurityHeaders.NonceItemKey]!;
        Assert.False(string.IsNullOrEmpty(nonce));
        Assert.Equal(SecurityHeaders.BuildPolicy(nonce), h["Content-Security-Policy"]);
        Assert.DoesNotContain("unsafe-inline", Assert.Single(h["Content-Security-Policy"]).Split("script-src")[1].Split(';')[0]);
        Assert.Contains("frame-ancestors 'none'", h["Content-Security-Policy"].ToString());
        Assert.Contains("script-src 'self'", h["Content-Security-Policy"].ToString());
        Assert.Equal("nosniff", h["X-Content-Type-Options"]);
        Assert.Equal("DENY", h["X-Frame-Options"]);
        Assert.False(string.IsNullOrEmpty(h["Referrer-Policy"]));
    }
}
