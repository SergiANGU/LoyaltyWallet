using System.Net;
using LoyaltyWallet.Cards;
using LoyaltyWallet.Google;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<WalletOptions>(builder.Configuration.GetSection("Wallet"));
builder.Services.AddSingleton(sp =>
    ServiceAccount.Load(sp.GetRequiredService<IOptions<WalletOptions>>().Value.ServiceAccountKeyPath));
builder.Services.AddSingleton<JwtSigner>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp => new GoogleAccessTokenProvider(
    sp.GetRequiredService<ServiceAccount>(),
    sp.GetRequiredService<JwtSigner>(),
    sp.GetRequiredService<IHttpClientFactory>().CreateClient()));
builder.Services.AddHttpClient<GoogleWalletClient>();
builder.Services.AddSingleton<CardStore>();

var app = builder.Build();

// Errores de Google como 502 con el mensaje original: más fácil de depurar en el test
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
    {
        ctx.Response.StatusCode = StatusCodes.Status502BadGateway;
        await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

// 1) Crear la plantilla (una sola vez)
app.MapPost("/setup/class", async (GoogleWalletClient wallet, CancellationToken ct) =>
    Results.Ok(new { message = await wallet.EnsureClassAsync(ct) }));

// 2) Dar de alta un cliente y su tarjeta
app.MapPost("/cards", async (CreateCardRequest req, CardStore store, GoogleWalletClient wallet, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest(new { error = "Name es obligatorio" });

    var member = store.Create(req.Name.Trim());
    await wallet.CreateObjectAsync(member.Id, member.Name, member.Points, ct);

    return Results.Created($"/cards/{member.Id}", new
    {
        member.Id, member.Name, member.Points,
        saveUrl = wallet.BuildSaveUrl(member.Id)
    });
});

app.MapGet("/cards/{id}", (string id, CardStore store, GoogleWalletClient wallet) =>
    store.Get(id) is { } m
        ? Results.Ok(new { m.Id, m.Name, m.Points, saveUrl = wallet.BuildSaveUrl(m.Id) })
        : Results.NotFound());

// Ábrelo desde el móvil: redirige a Google y muestra "Añadir a Google Wallet"
app.MapGet("/cards/{id}/save", (string id, CardStore store, GoogleWalletClient wallet) =>
    store.Get(id) is null ? Results.NotFound() : Results.Redirect(wallet.BuildSaveUrl(id)));

// 3) Sumar (o restar) puntos: la tarjeta se actualiza en el móvil
app.MapPost("/cards/{id}/points", async (string id, AddPointsRequest req, CardStore store, GoogleWalletClient wallet, CancellationToken ct) =>
{
    if (store.Get(id) is not { } member) return Results.NotFound();

    var updated = store.AddPoints(member, req.Amount);
    await wallet.UpdatePointsAsync(updated.Id, updated.Points, ct);
    return Results.Ok(updated);
});

// Página mínima para probar desde el navegador del móvil
app.MapGet("/", (CardStore store) =>
{
    var rows = string.Join("", store.All().Select(m =>
        $"""
        <li><b>{WebUtility.HtmlEncode(m.Name)}</b> · {m.Points} pts ·
          <a href="/cards/{m.Id}/save">Añadir a Google Wallet</a>
          <form method="post" action="/ui/points/{m.Id}" style="display:inline">
            <button name="amount" value="10">+10</button></form></li>
        """));

    return Results.Content($"""
        <!doctype html><html lang="es"><meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Test tarjeta fidelidad</title>
        <body style="font-family:system-ui;max-width:480px;margin:2rem auto;padding:0 1rem">
        <h1>Tarjeta de fidelidad</h1>
        <form method="post" action="/ui/cards"><input name="name" placeholder="Nombre" required>
          <button>Crear tarjeta</button></form>
        <ul>{rows}</ul></body></html>
        """, "text/html");
});

app.MapPost("/ui/cards", async (HttpRequest http, CardStore store, GoogleWalletClient wallet, CancellationToken ct) =>
{
    var name = (await http.ReadFormAsync(ct))["name"].ToString().Trim();
    if (name.Length == 0) return Results.Redirect("/");
    var member = store.Create(name);
    await wallet.CreateObjectAsync(member.Id, member.Name, member.Points, ct);
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapPost("/ui/points/{id}", async (string id, HttpRequest http, CardStore store, GoogleWalletClient wallet, CancellationToken ct) =>
{
    if (store.Get(id) is { } member && int.TryParse((await http.ReadFormAsync(ct))["amount"], out var amount))
    {
        var updated = store.AddPoints(member, amount);
        await wallet.UpdatePointsAsync(updated.Id, updated.Points, ct);
    }
    return Results.Redirect("/");
}).DisableAntiforgery();

app.Run();

public sealed record CreateCardRequest(string Name);
public sealed record AddPointsRequest(int Amount);
