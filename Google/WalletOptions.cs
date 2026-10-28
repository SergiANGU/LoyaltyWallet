namespace LoyaltyWallet.Google;

/// <summary>Sección "Wallet" de appsettings.json.</summary>
public sealed class WalletOptions
{
    /// <summary>Issuer ID de la Google Pay &amp; Wallet Console (solo números).</summary>
    public string IssuerId { get; set; } = "";

    /// <summary>Ruta a la clave JSON de la service account. No la subas nunca al repositorio.</summary>
    public string ServiceAccountKeyPath { get; set; } = "secrets/service-account.json";

    /// <summary>Sufijo del ID de la plantilla. El ID final es "{IssuerId}.{ClassSuffix}".</summary>
    public string ClassSuffix { get; set; } = "loyalty_test_v1";

    public string IssuerName { get; set; } = "Mi Negocio";
    public string ProgramName { get; set; } = "Club de fidelidad";

    /// <summary>URL pública HTTPS del logo (Google la descarga, no sirve localhost).</summary>
    public string ProgramLogoUrl { get; set; } = "";

    public string HexBackgroundColor { get; set; } = "#1f3b4d";

    public string ClassId => $"{IssuerId}.{ClassSuffix}";
    public string ObjectId(string memberId) => $"{IssuerId}.{memberId}";
}
