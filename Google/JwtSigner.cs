using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoyaltyWallet.Google;

/// <summary>
/// Firma JWT con RS256 usando la clave privada de la service account.
/// Se usa para dos cosas: pedir el access token OAuth y generar el enlace "Añadir a Google Wallet".
/// </summary>
public sealed class JwtSigner : IDisposable
{
    private readonly RSA _rsa;

    public JwtSigner(ServiceAccount account)
    {
        _rsa = RSA.Create();
        _rsa.ImportFromPem(account.PrivateKey); // la clave viene en PEM PKCS#8
    }

    public string Sign(object claims)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        var unsigned = $"{header}.{payload}";

        var signature = _rsa.SignData(
            Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{unsigned}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose() => _rsa.Dispose();
}
