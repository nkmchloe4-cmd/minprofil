using minprofil.Components;
using minprofil.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AppDatabase>();
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddScoped<CurrentUser>();

var app = builder.Build();

// Skapa och seeda databasen vid uppstart.
app.Services.GetRequiredService<AppDatabase>().EnsureCreatedAndSeeded();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>();

// ── Inloggning ─────────────────────────────────────────────────────────────
app.MapPost("/auth/login", (HttpContext http, AppDatabase db, SessionStore sessions,
    [Microsoft.AspNetCore.Mvc.FromForm] string username,
    [Microsoft.AspNetCore.Mvc.FromForm] string password) =>
{
    var user = db.Login(username, password);
    if (user is null)
    {
        // Samma väg tillbaka oavsett vad som gick fel.
        return Results.Redirect("/login?error=1");
    }

    var token = sessions.Create(user.Id);
    http.Response.Cookies.Append(CurrentUser.CookieName, token, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = true
    });

    return Results.Redirect("/profile");
}).DisableAntiforgery();

// ── Utloggning ─────────────────────────────────────────────────────────────
app.MapPost("/auth/logout", (HttpContext http, SessionStore sessions) =>
{
    var token = http.Request.Cookies[CurrentUser.CookieName];
    sessions.Invalidate(token);
    http.Response.Cookies.Delete(CurrentUser.CookieName);

    return Results.Redirect("/");
}).DisableAntiforgery();

// ── Spara profiltext ───────────────────────────────────────────────────────
app.MapPost("/profile/save", (AppDatabase db, CurrentUser current,
    [Microsoft.AspNetCore.Mvc.FromForm] string presentation) =>
{
    var user = current.Get();
    if (user is null)
    {
        return Results.Redirect("/login");
    }

    db.UpdatePresentation(user.Id, presentation);
    return Results.Redirect("/profile");
}).DisableAntiforgery();

app.Run();
