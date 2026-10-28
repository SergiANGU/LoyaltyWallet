# LoyaltyWallet: test de tarjeta de fidelidad en Google Wallet

Minimal API en ASP.NET Core (.NET 8) **sin dependencias NuGet**. La firma JWT (RS256) y el token OAuth de la service account se hacen con `System.Security.Cryptography`.

## 1. Preparar Google (una sola vez)

1. **Google Cloud Console** → crea un proyecto → *APIs & Services* → activa **Google Wallet API**.
2. *IAM & Admin → Service accounts* → crea una service account → *Keys → Add key → JSON*.
   Guarda el fichero como `secrets/service-account.json` (está en `.gitignore`).
3. **Google Pay & Wallet Console** (pay.google.com/business/console) → *Google Wallet API* → copia tu **Issuer ID**.
4. En la misma consola → *Users* → invita al email de la service account (`...@...iam.gserviceaccount.com`) con permiso de **Developer**.
5. Mientras la cuenta esté en **modo demo**, solo las cuentas de Google añadidas como usuarios de prueba pueden guardar la tarjeta. Añade la tuya.

## 2. Configurar

En `appsettings.json` (o `dotnet user-secrets`):

```json
"Wallet": {
  "IssuerId": "3388000000012345678",
  "ServiceAccountKeyPath": "secrets/service-account.json",
  "ProgramLogoUrl": "https://..."   // URL pública HTTPS, Google la descarga
}
```

## 3. Ejecutar

```bash
dotnet run --urls http://0.0.0.0:5099
```

Escuchar en `0.0.0.0` permite abrirla desde el móvil en la misma Wi-Fi: `http://IP-DE-TU-PC:5099`.

## 4. Probar

```bash
# Crear la plantilla (una vez)
curl -X POST http://localhost:5099/setup/class

# Alta de cliente → devuelve saveUrl
curl -X POST http://localhost:5099/cards -H "Content-Type: application/json" -d '{"name":"Sergi"}'

# Sumar puntos → la tarjeta se actualiza sola en el móvil
curl -X POST http://localhost:5099/cards/{id}/points -H "Content-Type: application/json" -d '{"amount":10}'
```

O abre `http://IP-DE-TU-PC:5099/` desde el Android: crear tarjeta, botón *Añadir a Google Wallet* y botón *+10*.

| Endpoint | Qué hace |
|---|---|
| `POST /setup/class` | Crea la `LoyaltyClass` (plantilla). Idempotente. |
| `POST /cards` | Crea el cliente y su `LoyaltyObject` y devuelve `saveUrl`. |
| `GET /cards/{id}` | Datos del cliente y `saveUrl`. |
| `GET /cards/{id}/save` | Redirige al enlace de Google Wallet. |
| `POST /cards/{id}/points` | Suma o resta puntos con un `PATCH` al objeto. |

## Estructura

```
Google/
  ServiceAccount.cs              lee la clave JSON
  JwtSigner.cs                   firma RS256
  GoogleAccessTokenProvider.cs   OAuth JWT-bearer + caché del token
  GoogleWalletClient.cs          REST: loyaltyClass, loyaltyObject, enlace de guardado
  WalletOptions.cs               configuración
Cards/CardStore.cs               almacén en memoria (se pierde al reiniciar)
Program.cs                       endpoints + página de prueba
```

## Problemas habituales

- **403 de la API**: la service account no está añadida como usuario en la Pay & Wallet Console, o el Issuer ID no es el correcto.
- **"This pass can't be added"** en el móvil: tu cuenta de Google no es usuario de prueba (modo demo), o el logo no es una URL HTTPS pública.
- **Cambias la plantilla y no se refleja**: cambia `ClassSuffix` (p. ej. `loyalty_test_v2`) para crear otra, o haz un `PATCH` a la clase.
