using BlazerSignalRChat.Chat;
using BlazerSignalRChat.Components;
using BlazerSignalRChat.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();
builder.Services.AddSingleton<ChatState>();
builder.Services.AddScoped<SessionState>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHub<ChatHub>("/chat");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();