using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoyaltyWallet.Google;

/// <summary>
/// Datos que necesitamos de la clave JSON de la service account de Google Cloud.
/// </summary>
public sealed record ServiceAccount(
    [property: JsonPropertyName("client_email")] string ClientEmail,
    [property: JsonPropertyName("private_key")] string PrivateKey,
    [property: JsonPropertyName("token_uri")] string TokenUri)
{
    public static ServiceAccount Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"No se encuentra la clave de la service account en '{path}'. " +
                "Descárgala desde Google Cloud (IAM > Service accounts > Keys) y ajusta Wallet:ServiceAccountKeyPath.");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ServiceAccount>(json)
               ?? throw new InvalidOperationException("La clave de la service account no es válida.");
    }
}
