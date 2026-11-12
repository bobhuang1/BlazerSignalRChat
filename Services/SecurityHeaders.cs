namespace BlazerSignalRChat.Services;

/// <summary>Adds a Content Security Policy and basic hardening headers to every response.</summary>
public static class SecurityHeaders
{
    // 'self' covers same-origin fetch and websockets in modern browsers; ws:/wss: are listed
    // explicitly for older browsers that do not treat 'self' as matching the websocket scheme.
    // style-src allows inline style attributes (--hue, --accent, --sw custom properties).
    // script-src also allows a per-request nonce, used only by the inline import map that
    // Blazor needs for fingerprinted assets (see App.razor). No other inline script is allowed.
    public const string NonceItemKey = "csp-nonce";

    public static string BuildPolicy(string nonce) =>
        "default-src 'self'; " +
        $"script-src 'self' 'nonce-{nonce}'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self' ws: wss:; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var nonce = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
            context.Items[NonceItemKey] = nonce;
            var h = context.Response.Headers;
            h["Content-Security-Policy"] = BuildPolicy(nonce);
            h["X-Content-Type-Options"] = "nosniff";
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            h["X-Frame-Options"] = "DENY";
            await next();
        });
}
