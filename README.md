# 📄 Anonimizador Documental — CGR

Sistema de anonimización de documentos jurídicos desarrollado para la **Contraloría General de la República de Costa Rica**. Reemplaza datos sensibles y personales por etiquetas neutrales, siguiendo la normativa **PRODHAB** sobre datos personales y sensibles.

---

## ✨ Funcionalidades principales

- 🔐 Autenticación JWT con roles (Admin / Operator)
- 📄 Anonimización de documentos `.docx` y `.pdf`
- 🤖 Detección híbrida: **Regex preciso + IA semántica** (Ollama / Mistral)
- 👥 Soporte para múltiples personas por documento
- 🏛️ Campos PRODHAB: datos personales y datos sensibles (cuenta bancaria, condición médica)
- 📊 Dashboard con historial, métricas y gráficos
- 🔍 Auditoría granular por campo anonimizado
- 🌐 Landing page pública de presentación
- 📱 Interfaz wizard paso a paso

---

## 🧠 Arquitectura

### API REST

```text
API (Controllers)
    ↓
CrossCutting (Middleware: Errors, CorrelationId)
    ↓
Application (Services, DTOs, Common)
    ↓
Interfaces (Contracts)
    ↓
Infrastructure (Repositories, Data)
    ↓
SQL Server (Stored Procedures)
```

### Frontend Web (MVC)

```text
Browser
    ↓
Landing / Login / Wizard / Dashboard (Razor Views)
    ↓
Controllers (Auth, Home, Upload, Ai, Dashboard, Landing)
    ↓
HttpClient → API REST
```

---

## 🔄 Flujo del sistema

```text
1. Usuario visita /landing → presentación del sistema
2. Login valida credenciales contra la API
3. Token JWT se guarda en cookie cifrada (30 min)
4. Usuario sube documento y elige modo:
   a. Manual   → ingresa datos directamente
   b. IA       → Regex + Ollama detectan automáticamente
5. Web envía el request a la API con el token
6. API valida token y rol
7. API procesa:
   ├── Valida archivo (.docx o .pdf, tamaño, estructura)
   ├── Calcula hash SHA256 del original
   ├── Registra proceso en BD (estado: PROCESSING)
   ├── Anonimiza en memoria (sin guardar en disco)
   │   ├── DOCX: párrafos, tablas, headers, footers,
   │   │         textboxes VML/Drawing, tracking changes
   │   └── PDF:  redacción por imagen (sin capa de texto)
   ├── Registra versión ANONYMIZED con hash en BD
   ├── Registra auditoría campo por campo
   └── Actualiza estado (ANONYMIZED)
8. API retorna el documento como stream
9. Web muestra vista previa (PDF) o mensaje (DOCX)
10. Usuario descarga el archivo anonimizado
```

---

## 🧩 Tecnologías

| Capa | Tecnología |
|---|---|
| API | .NET 8, ASP.NET Core Web API |
| Frontend | .NET 8, ASP.NET Core MVC, Razor |
| Base de datos | SQL Server |
| ORM | Dapper |
| Documentos Word | OpenXML SDK |
| Documentos PDF | PdfPig, PdfSharp, PDFtoImage, SkiaSharp |
| IA | Ollama (Mistral) |
| Autenticación | JWT Bearer + Cookie Authentication |
| Passwords | BCrypt.Net (cost factor 12) |
| Swagger | Swashbuckle.AspNetCore |

---

## 📁 Estructura del proyecto

```text
/
├── Anonimizador - API/
│   ├── API/Controllers/
│   │   ├── AuthController.cs
│   │   └── DocumentsController.cs
│   ├── Application/
│   │   ├── Common/
│   │   │   ├── PdfLineInfo.cs
│   │   │   ├── PdfRedactionInfo.cs
│   │   │   ├── PdfWordInfo.cs
│   │   │   ├── RegexCatalog.cs
│   │   │   └── TextAnonymizationEngine.cs
│   │   ├── DTOs/
│   │   │   ├── Analysis/DocumentAnalysisDto.cs
│   │   │   ├── Auth/LoginRequestDto.cs
│   │   │   ├── Auth/LoginResponseDto.cs
│   │   │   ├── Auth/UserDto.cs
│   │   │   ├── Documents/AnonymizationResultDto.cs
│   │   │   ├── Documents/AnonymizationTargetDto.cs
│   │   │   ├── Documents/DocumentSummaryDto.cs
│   │   │   ├── Documents/UploadDocumentRequestDto.cs
│   │   │   └── Metrics/MetricsDto.cs
│   │   └── Services/
│   │       ├── Analysis/DocumentAnalysisService.cs
│   │       ├── Analysis/OllamaService.cs
│   │       ├── Auth/AuthService.cs
│   │       ├── Documents/DocumentService.cs
│   │       └── Processors/
│   │           ├── PdfDocumentProcessor.cs
│   │           └── WordDocumentProcessor.cs
│   ├── CrossCutting/
│   │   ├── CorrelationIdMiddleware.cs
│   │   └── ExceptionMiddleware.cs
│   ├── Domain/Entities/Document.cs
│   ├── Infrastructure/
│   │   ├── Data/DbConnectionFactory.cs
│   │   └── Repositories/
│   │       ├── DocumentRepository.cs
│   │       └── UserRepository.cs
│   └── Interfaces/
│       ├── Repositories/
│       └── Services/
│
├── Anonimizador - Web/
│   ├── Controllers/
│   │   ├── AiController.cs
│   │   ├── AuthController.cs
│   │   ├── DashboardController.cs
│   │   ├── HomeController.cs
│   │   ├── LandingController.cs
│   │   └── UploadController.cs
│   ├── Models/
│   │   ├── DashboardViewModel.cs
│   │   ├── ErrorViewModel.cs
│   │   ├── LoginViewModel.cs
│   │   └── MetricsViewModel.cs
│   └── Views/
│       ├── Auth/Login.cshtml
│       ├── Dashboard/Index.cshtml
│       ├── Home/Index.cshtml
│       ├── Landing/Index.cshtml
│       └── Shared/_Layout.cshtml
│
└── DB/
    ├── DocumentAnonymizerDB.sql    ← Script base
    ├── CHANGELOG.md                ← Historial de migraciones
    └── Migrations/
        ├── 001_AddFullNameToUsers.sql
        └── 002_ExpandAnonymizationFields.sql
```

---

## 🗄️ Base de datos

### Tablas

| Tabla | Descripción |
|---|---|
| `DOCUMENTS` | Metadata de cada documento procesado |
| `DOCUMENT_VERSIONS` | Versiones del documento (ORIGINAL / ANONYMIZED) |
| `ANONYMIZED_FIELDS` | Auditoría campo por campo |
| `PROCESS_STATUS` | Catálogo de estados |
| `PROCESS_ERRORS` | Registro centralizado de errores |
| `USERS` | Usuarios del sistema |
| `ROLES` | Roles (Admin, Operator) |

### Estados del proceso

```text
1 → UPLOADED
2 → PROCESSING
3 → ANONYMIZED
4 → FAILED
```

### Migraciones

El proyecto usa un sistema de migraciones manual en `DB/Migrations/`. Cada migración es idempotente — se puede ejecutar múltiples veces sin romper nada. Ver `DB/CHANGELOG.md` para el estado de cada migración.

---

## 🔒 Campos de anonimización

### Por persona

| Campo | Etiqueta | Clasificación |
|---|---|---|
| Nombre completo | `[Px-Nombre]` | Personal |
| Identificación | `[Px-Cédula]` | Personal |
| Correo electrónico | `[Px-Correo]` | Personal |
| Teléfono | `[Px-Tel]` | Personal |
| Cargo o puesto | `[Px-Cargo]` | Personal |
| Dirección | `[Px-Dir]` | Personal |
| Institución | `[Px-Institución]` | Personal |
| Cuenta bancaria | `[Px-CuentaBancaria]` | Sensible (PRODHAB) |
| Condición médica | `[Px-CondiciónMédica]` | Sensible (PRODHAB) |
| Texto libre | `[Px-Dato]` | Libre |

### Generales del documento

| Campo | Etiqueta |
|---|---|
| Número de expediente | `[Expediente]` |
| Número de oficio | `[N° Oficio]` |

### Variaciones por campo

Se pueden agregar variaciones de formato para Nombre, Cédula y Teléfono. Ejemplo: `1-2345-6789` y su variación `123456789` se reemplazan con la misma etiqueta.

---

## 🤖 Motor de detección

### Regex (RegexCatalog)
Detecta patrones exactos: cédulas costarricenses (`X-XXXX-XXXX`), correos, teléfonos (`XXXX-XXXX`) y nombres.

### IA — Ollama / Mistral
Análisis semántico del documento completo. Detecta: nombres, cédulas, correos, teléfonos, cargos, direcciones, instituciones, cuentas bancarias y condiciones médicas. Retorna resultados en formato estructurado para mayor robustez.

Los resultados de ambos motores se fusionan evitando duplicados.

---

## 🖥️ Motor de anonimización DOCX

| Zona | Cubierta |
|---|---|
| Párrafos del cuerpo | ✅ |
| Tablas | ✅ |
| Encabezados (Headers) | ✅ |
| Pies de página (Footers) | ✅ |
| Textboxes VML | ✅ |
| Textboxes Drawing | ✅ |
| Cambios rastreados (ins/del) | ✅ |

## 🖥️ Motor de anonimización PDF

Redacción basada en imagen: el PDF se renderiza a 250 DPI, se aplican rectángulos de redacción sobre los píxeles y se reconstruye el PDF sin capa de texto. Los datos originales no son recuperables.

---

## 🔐 Seguridad

- JWT Bearer en la API con validación de issuer, audience y tiempo de vida
- Cookie cifrada en el Web (HttpOnly, Secure, 30 min)
- Timer de sesión con warning a los 25 min — persiste entre navegaciones
- Passwords con BCrypt (cost factor 12)
- Roles: `Admin`, `Operator`
- Correlation ID en cada request para trazabilidad
- Middleware global de manejo de errores con detalle solo en Development
- Rate limiting por IP: 10 intentos/min en login, 30 requests/min en documentos
- Páginas de error personalizadas (404, 403, 500)
- Logs estructurados con Serilog (consola + archivo rotativo diario)

---

## ⚙️ Configuración

### Requisitos

- .NET 8 SDK
- SQL Server
- Ollama con Mistral instalado (`ollama pull mistral`)

### API — `appsettings.json`

Usar `appsettings.example.json` como referencia:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=DocumentAnonymizerDB;Trusted_Connection=True;"
  },
  "Jwt": {
    "Key": "YOUR_SECRET_KEY_MIN_32_CHARS",
    "Issuer": "DocumentAnonymizer",
    "Audience": "DocumentAnonymizerUsers",
    "ExpirationHours": "8"
  },
  "Ollama": {
    "BaseUrl": "http://127.0.0.1:11434",
    "Model": "mistral",
    "TimeoutSeconds": "120"
  }
}
```

### Web — `appsettings.json`

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:YOUR_API_PORT"
  }
}
```

## 🔐 Variables de entorno (Producción)

En producción los secretos se configuran como variables de entorno
en lugar del `appsettings.json`. .NET lee ambas automáticamente,
dando prioridad a las variables de entorno.

La convención es reemplazar `:` por `__` (doble guion bajo):

### API

| Variable de entorno | Descripción |
|---|---|
| `ConnectionStrings__DefaultConnection` | Cadena de conexión SQL Server |
| `Jwt__Key` | Clave secreta JWT (mín. 32 caracteres) |
| `Jwt__Issuer` | Issuer del token |
| `Jwt__Audience` | Audience del token |
| `Jwt__ExpirationHours` | Duración del token en horas |
| `Ollama__BaseUrl` | URL de Ollama |
| `Ollama__Model` | Modelo a usar (ej. mistral) |
| `RateLimiting__Login__PermitLimit` | Intentos de login por minuto |
| `RateLimiting__Documents__PermitLimit` | Requests de documentos por minuto |

### Web

| Variable de entorno | Descripción |
|---|---|
| `ApiSettings__BaseUrl` | URL base de la API |

### Ejemplo en Windows (IIS / servidor)
\```
setx Jwt__Key "tu_clave_secreta_de_produccion" /M
setx ConnectionStrings__DefaultConnection "Server=prod-server;..." /M
\```

### Ejemplo en Linux / Docker
\```bash
export Jwt__Key="tu_clave_secreta_de_produccion"
export ConnectionStrings__DefaultConnection="Server=prod-server;..."
\```

---

## 🚀 Instalación

```bash
# 1. Clonar el repositorio
git clone https://github.com/Brandruiz7/document-anonymizer

# 2. Configurar appsettings en API y Web
# (copiar appsettings.example.json → appsettings.json y completar)

# 3. Ejecutar script de BD en SQL Server
# DB/DocumentAnonymizerDB.sql

# 4. Ejecutar migraciones pendientes
# DB/Migrations/001_AddFullNameToUsers.sql
# DB/Migrations/002_ExpandAnonymizationFields.sql

# 5. Iniciar Ollama con Mistral
ollama serve
ollama pull mistral

# 6. Correr la API
cd "Anonimizador - API"
dotnet run

# 7. Correr el Web
cd "Anonimizador - Web"
dotnet run
```

### Usuario administrador por defecto

```
Usuario:    admin
Contraseña: Admin123!
```

> ⚠️ Cambiar en producción generando un nuevo hash con el endpoint:
> `GET /api/auth/generate-hash?password=TuPassword`

---

## 📊 Auditoría

Cada documento genera registros en:

- `DOCUMENTS` — metadata y estado del proceso
- `DOCUMENT_VERSIONS` — versión anonimizada con hash SHA256
- `ANONYMIZED_FIELDS` — cada campo reemplazado con valor original, etiqueta y método de detección

---

## 👨‍💻 Autor

Ruiz — Ingeniero en Sistemas