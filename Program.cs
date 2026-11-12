using BlazerSignalRChat.Chat;
using BlazerSignalRChat.Components;
using BlazerSignalRChat.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = ChatLimits.MaxReceiveMessageSize);
builder.Services.AddSingleton<ChatState>();
builder.Services.AddSingleton<HubRateLimiter>();
builder.Services.AddSingleton<TypingTimeouts>();
builder.Services.AddScoped<SessionState>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHub<ChatHub>("/chat");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();