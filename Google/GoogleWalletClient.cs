using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace LoyaltyWallet.Google;

/// <summary>
/// Llamadas REST a la Google Wallet API para tarjetas de fidelidad.
/// Docs: https://developers.google.com/wallet/retail/loyalty-cards
/// </summary>
public sealed class GoogleWalletClient(
    HttpClient http,
    GoogleAccessTokenProvider tokens,
    JwtSigner signer,
    ServiceAccount account,
    IOptions<WalletOptions> options,
    ILogger<GoogleWalletClient> logger)
{
    private const string BaseUrl = "https://walletobjects.googleapis.com/walletobjects/v1";
    private readonly WalletOptions _opt = options.Value;

    // ---------- Plantilla (LoyaltyClass) ----------

    /// <summary>Crea la plantilla si no existe. Idempotente.</summary>
    public async Task<string> EnsureClassAsync(CancellationToken ct = default)
    {
        var classId = _opt.ClassId;

        using (var get = await SendAsync(HttpMethod.Get, $"loyaltyClass/{classId}", null, ct))
        {
            if (get.IsSuccessStatusCode) return $"La plantilla {classId} ya existía.";
            if (get.StatusCode != HttpStatusCode.NotFound) await ThrowAsync(get, ct);
        }

        var loyaltyClass = new
        {
            id = classId,
            issuerName = _opt.IssuerName,
            programName = _opt.ProgramName,
            programLogo = new
            {
                sourceUri = new { uri = _opt.ProgramLogoUrl },
                contentDescription = new { defaultValue = new { language = "es-ES", value = "Logo" } }
            },
            hexBackgroundColor = _opt.HexBackgroundColor,
            reviewStatus = "UNDER_REVIEW"
        };

        using var post = await SendAsync(HttpMethod.Post, "loyaltyClass", loyaltyClass, ct);
        if (!post.IsSuccessStatusCode) await ThrowAsync(post, ct);

        logger.LogInformation("Plantilla {ClassId} creada", classId);
        return $"Plantilla {classId} creada.";
    }

    // ---------- Tarjeta de cada cliente (LoyaltyObject) ----------

    public async Task CreateObjectAsync(string memberId, string memberName, int points, CancellationToken ct = default)
    {
        var loyaltyObject = new
        {
            id = _opt.ObjectId(memberId),
            classId = _opt.ClassId,
            state = "ACTIVE",
            accountId = memberId,
            accountName = memberName,
            loyaltyPoints = Points(points),
            barcode = new { type = "QR_CODE", value = memberId, alternateText = memberId }
        };

        using var response = await SendAsync(HttpMethod.Post, "loyaltyObject", loyaltyObject, ct);
        // 409 = ya existe; para un test lo damos por bueno
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Conflict)
            await ThrowAsync(response, ct);
    }

    /// <summary>Actualiza los puntos. El cambio llega solo al móvil del cliente.</summary>
    public async Task UpdatePointsAsync(string memberId, int points, CancellationToken ct = default)
    {
        using var response = await SendAsync(
            HttpMethod.Patch, $"loyaltyObject/{_opt.ObjectId(memberId)}", new { loyaltyPoints = Points(points) }, ct);
        if (!response.IsSuccessStatusCode) await ThrowAsync(response, ct);
    }

    /// <summary>
    /// Enlace "Añadir a Google Wallet". El objeto ya existe en Google, así que el JWT solo lleva su ID.
    /// </summary>
    public string BuildSaveUrl(string memberId)
    {
        var jwt = signer.Sign(new
        {
            iss = account.ClientEmail,
            aud = "google",
            typ = "savetowallet",
            iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            origins = Array.Empty<string>(),
            payload = new
            {
                loyaltyObjects = new[] { new { id = _opt.ObjectId(memberId) } }
            }
        });

        return $"https://pay.google.com/gp/v/save/{jwt}";
    }

    // ---------- Helpers ----------

    private static object Points(int points) => new { label = "Puntos", balance = new { @int = points } };

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, $"{BaseUrl}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(ct));
        if (body is not null) request.Content = JsonContent.Create(body);
        return await http.SendAsync(request, ct);
    }

    private static async Task ThrowAsync(HttpResponseMessage response, CancellationToken ct) =>
        throw new InvalidOperationException(
            $"Google Wallet API devolvió {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
}
