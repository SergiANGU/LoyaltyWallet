using System.Text.Json.Serialization;

namespace LoyaltyWallet.Google;

/// <summary>
/// Obtiene (y cachea) un access token OAuth 2.0 con el flujo "JWT bearer" de service accounts.
/// Es lo mismo que hace Google.Apis.Auth por dentro.
/// </summary>
public sealed class GoogleAccessTokenProvider(
    ServiceAccount account,
    JwtSigner signer,
    HttpClient http)
{
    private const string Scope = "https://www.googleapis.com/auth/wallet_object.issuer";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt) return _token;

        await _lock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt) return _token;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var assertion = signer.Sign(new
            {
                iss = account.ClientEmail,
                scope = Scope,
                aud = account.TokenUri,
                iat = now,
                exp = now + 3600
            });

            using var response = await http.PostAsync(account.TokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion
            }), ct);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Google rechazó la autenticación ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(ct)}");

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
                       ?? throw new InvalidOperationException("Respuesta de token vacía.");

            _token = body.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(body.ExpiresIn - 60); // margen de 1 min
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
