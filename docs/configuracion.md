# ⚙️ Configuración

[← Volver al README principal](../README.md)

## appsettings.json

Usar `appsettings.example.json` como referencia. El archivo real nunca se sube al repositorio (está en `.gitignore`).

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "User Id=anonimizador;Password=TuPassword;Data Source=localhost:1521/XEPDB1;"
  },
  "Jwt": {
    "Key": "clave-secreta-minimo-32-caracteres",
    "Issuer": "DocumentAnonymizer",
    "Audience": "DocumentAnonymizerUsers",
    "ExpirationHours": "8"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "mistral",
    "TimeoutSeconds": "120"
  },
  "Gemini": {
    "ApiKey": "tu-api-key-de-google",
    "Model": "gemini-2.0-flash"
  },
  "RateLimiting": {
    "Login":     { "PermitLimit": 10, "WindowMinutes": 1 },
    "Documents": { "PermitLimit": 30, "WindowMinutes": 1 }
  },
  // CORS: Solo necesario si el browser llama directamente a la API.
  // No aplica cuando todos los llamados van vía APEX server-side (UTL_HTTP).
  // "Cors": {
  //   "AllowedOrigins": [ "http://localhost:8080" ]
  // },
  "Serilog": {
    "MinimumLevel": "Information",
    "RetainedFileDays": 30
  }
}
```

> **Nota sobre CORS:** en esta arquitectura el browser nunca llama directamente a la API — todos los requests van server-side desde Oracle APEX via `UTL_HTTP` en el package `PKG_WIZARD_ANON`. Por ese motivo `AddCors()` y `app.UseCors()` fueron eliminados de `Program.cs` y la sección `Cors` en `appsettings` está comentada. Si en el futuro se necesita exponer la API directamente al browser (ej: una app web externa), descomentar ambas secciones y agregar los orígenes permitidos.

---

## Variables de entorno (Producción)

En producción los secretos se configuran como variables de entorno. .NET les da prioridad sobre `appsettings.json`. La convención es reemplazar `:` por `__` (doble guion bajo):

| Variable | Descripción |
|---|---|
| `ConnectionStrings__DefaultConnection` | Cadena de conexión Oracle |
| `Jwt__Key` | Clave secreta JWT (mín. 32 caracteres) |
| `Jwt__Issuer` | Issuer del token |
| `Jwt__Audience` | Audience del token |
| `Jwt__ExpirationHours` | Duración del token en horas |
| `Ollama__BaseUrl` | URL del servidor Ollama |
| `Ollama__Model` | Modelo a usar (ej. mistral) |
| `Gemini__ApiKey` | API Key de Google Gemini |
| `Gemini__Model` | Modelo Gemini (ej. gemini-2.0-flash) |
| `RateLimiting__Login__PermitLimit` | Intentos de login por minuto |
| `RateLimiting__Documents__PermitLimit` | Requests de documentos por minuto |

### Ejemplo en Windows
```cmd
setx Jwt__Key "tu-clave-secreta-produccion" /M
setx ConnectionStrings__DefaultConnection "User Id=...;Password=...;Data Source=..." /M
```

### Ejemplo en Linux / Docker
```bash
export Jwt__Key="tu-clave-secreta-produccion"
export ConnectionStrings__DefaultConnection="User Id=...;Password=...;Data Source=..."
```

---

## Motor de IA — cambiar entre Ollama y Gemini

El motor activo se controla en tres archivos:

### 1. `DocumentAnalysisService.cs`
```csharp
// Ollama activo (por defecto):
private readonly OllamaService _ollama;
// private readonly GeminiService _gemini;

// Para Gemini:
// private readonly OllamaService _ollama;
private readonly GeminiService _gemini;
```

### 2. `Program.cs`
```csharp
// Ollama activo (por defecto):
builder.Services.AddSingleton<OllamaService>();
// builder.Services.AddSingleton<GeminiService>();

// Para Gemini:
// builder.Services.AddSingleton<OllamaService>();
builder.Services.AddSingleton<GeminiService>();
```

### 3. `appsettings.json`
Asegurate de tener configurada la sección correspondiente al motor activo.

---

## Oracle XE 21c — configuración del listener

Si `XEPDB1` no aparece en el listener al instalar Oracle XE:

```cmd
# Abrir CMD como administrador
sqlplus / as sysdba
```

```sql
ALTER SYSTEM SET LOCAL_LISTENER = '(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))' SCOPE=BOTH;
ALTER SYSTEM REGISTER;
EXIT;
```

```cmd
lsnrctl stop
lsnrctl start
```

---

## Cadena de conexión Oracle

```
User Id=anonimizador;Password=TuPassword;Data Source=localhost:1521/XEPDB1;
```

Para producción reemplazar `localhost` por el host del servidor Oracle institucional.

---

## Cadena de conexión SQL Server (referencia)

```
Server=.;Database=DocumentAnonymizerDB;Trusted_Connection=True;
```

Ver [Base de datos](base-de-datos.md) para los archivos necesarios al usar SQL Server.

---

## Ver también

- [Seguridad](seguridad.md)
- [Base de datos](base-de-datos.md)