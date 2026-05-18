# 📄 Anonimizador documental

Sistema para la **anonimización de documentos Word** con autenticación JWT, trazabilidad completa, auditoría por campo y frontend web integrado.

---

## 🚀 Descripción

Este proyecto implementa una solución completa compuesta por:

- **API REST** desarrollada en **.NET 8** para procesamiento y anonimización de documentos
- **Frontend Web** desarrollado en **.NET 8 MVC con Razor** para uso interno de la organización

Funcionalidades principales:

- Autenticación JWT con roles (Admin / Operator)
- Subida y anonimización de documentos `.docx` en memoria
- Detección y reemplazo de datos sensibles por campo
- Auditoría granular de cada campo anonimizado
- Trazabilidad completa del proceso con estados
- Registro centralizado de errores con Correlation IDs
- Motor de anonimización que cubre: párrafos, tablas, headers, footers, textboxes y tracking changes

---

## 🧠 Arquitectura

### API

```text
API (Controllers)
    ↓
CrossCutting (Middleware: Errors, CorrelationId)
    ↓
Application (Services, DTOs)
    ↓
Interfaces (Contracts)
    ↓
Infrastructure (Repositories, Data)
    ↓
Database (SQL Server)
```

### Frontend Web (MVC)

```text
Browser
    ↓
Controllers (Auth, Home, Upload)
    ↓
Views (Razor)
    ↓
HttpClient → API REST
```

---

## 🔄 Flujo del sistema

```text
1. Usuario accede al Web → redirige a Login
2. Login valida credenciales contra la API
3. Token JWT se guarda en cookie cifrada
4. Usuario sube documento + datos a anonimizar
5. Web envía el request a la API con el token
6. API valida token y rol
7. API procesa:
    - Valida archivo (.docx, tamaño, estructura)
    - Genera hash SHA256 del original
    - Registra proceso en BD (estado: PROCESSING)
    - Anonimiza en memoria (sin guardar en disco)
    - Genera hash SHA256 del anonimizado
    - Registra versión ANONYMIZED en BD
    - Registra cada campo reemplazado en auditoría
    - Actualiza estado (ANONYMIZED)
8. API retorna el documento anonimizado como stream
9. Web descarga el archivo automáticamente al usuario
```

---

## 🧩 Tecnologías utilizadas

| Capa | Tecnología |
|---|---|
| API | .NET 8, ASP.NET Core Web API |
| Frontend | .NET 8, ASP.NET Core MVC, Razor |
| Base de datos | SQL Server |
| ORM | Dapper |
| Documentos | OpenXML SDK |
| Autenticación | JWT Bearer + Cookie Authentication |
| Passwords | BCrypt.Net |
| Swagger | Swashbuckle.AspNetCore |

---

## 📁 Estructura del proyecto

```text
Anonimizador de datos/
├── Anonimizador___API/          ← API REST
│   ├── API/
│   │   └── Controllers/
│   │       ├── AuthController.cs
│   │       └── DocumentsController.cs
│   ├── Application/
│   │   ├── Common/
│   │   │   └── RegexCatalog.cs
│   │   ├── DTOs/
│   │   └── Services/
│   │       ├── AnonymizationService.cs
│   │       ├── AuthService.cs
│   │       └── DocumentService.cs
│   ├── CrossCutting/
│   │   ├── CorrelationIdMiddleware.cs
│   │   └── ExceptionMiddleware.cs
│   ├── Domain/
│   │   └── Entities/
│   ├── Infrastructure/
│   │   ├── Data/
│   │   └── Repositories/
│   └── Interfaces/
│       ├── Repositories/
│       └── Services/
├── Anonimizador___Web/      ← Frontend MVC
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── HomeController.cs
│   │   └── UploadController.cs
│   ├── Models/
│   │   └── LoginViewModel.cs
│   └── Views/
│       ├── Auth/
│       │   └── Login.cshtml
│       ├── Home/
│       │   └── Index.cshtml
│       └── Shared/
│           └── _Layout.cshtml
└── DocumentAnonymizerDB.sql     ← Script completo de BD
```

---

## 🗄️ Base de datos

### Tablas principales

| Tabla | Descripción |
|---|---|
| `DOCUMENTS` | Metadata de cada documento procesado |
| `DOCUMENT_VERSIONS` | Versiones del documento (ANONYMIZED) |
| `ANONYMIZED_FIELDS` | Auditoría campo por campo |
| `PROCESS_STATUS` | Catálogo de estados (UPLOADED, PROCESSING, ANONYMIZED, FAILED) |
| `PROCESS_ERRORS` | Registro centralizado de errores |
| `USERS` | Usuarios del sistema (passwords con BCrypt) |
| `ROLES` | Roles (Admin, Operator) |

### Estados del proceso

```text
1 → UPLOADED
2 → PROCESSING
3 → ANONYMIZED
4 → FAILED
```

---

## 🔐 Seguridad

- Autenticación via JWT Bearer en la API
- Sesión en el Web via Cookie cifrada con DPAPI
- Passwords almacenadas con BCrypt (cost factor 12)
- Roles: `Admin`, `Operator`
- Correlation ID en cada request para trazabilidad
- Middleware global de manejo de errores

---

## ⚙️ Configuración

### API — `appsettings.json`

Usar `appsettings.example.json` como referencia:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=DocumentAnonymizerDB;..."
  },
  "Jwt": {
    "Key": "YOUR_SECRET_KEY_MIN_32_CHARS",
    "Issuer": "DocumentAnonymizer",
    "Audience": "DocumentAnonymizerUsers",
    "ExpirationHours": 8
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

---

## 🚀 Instalación

### Requisitos

- .NET 8 SDK
- SQL Server
- Visual Studio 2022 o VS Code

### Pasos

1. Clonar el repositorio
2. Crear `appsettings.json` en ambos proyectos basándose en `appsettings.example.json`
3. Ejecutar `DocumentAnonymizerDB.sql` en SQL Server
4. Generar hash del password admin:
   ```
   GET /api/auth/generate-hash?password=TuPassword
   ```
5. Actualizar el hash en la tabla `USERS`
6. Correr la API y el Web

---

## 🔍 Motor de anonimización

El motor cubre todas las zonas del documento Word:

| Zona | Cubierta |
|---|---|
| Párrafos del cuerpo | ✅ |
| Tablas | ✅ |
| Headers | ✅ |
| Footers | ✅ |
| Textboxes clásicos (VML) | ✅ |
| Textboxes modernos (Drawing) | ✅ |
| Tracking Changes (w:ins / w:del) | ✅ |

Campos que puede anonimizar:

| Campo | Reemplazo |
|---|---|
| Nombre completo | `[NAME]` |
| Identificación | `[ID]` |
| Correo electrónico | `[EMAIL]` |
| Teléfono | `[PHONE]` |
| Cargo o puesto | `[POSITION]` |
| Dirección | `[ADDRESS]` |

---

## 📊 Auditoría

Cada documento procesado genera registros en:

- `DOCUMENTS` — metadata y estado
- `DOCUMENT_VERSIONS` — versión anonimizada con hash
- `ANONYMIZED_FIELDS` — cada campo reemplazado con valor original, reemplazo y método de detección

---

## 🛠 Estado del proyecto

### ✅ Completado

- Middleware global (errores + correlation IDs)
- Motor DOCX completo (tablas, textboxes, tracking changes)
- JWT + roles
- Auditoría profesional por campo
- Streaming seguro (sin guardar archivos en disco)
- Frontend Web con login, formulario y descarga automática

### ⬜ Próximos pasos

- Dashboard con historial de documentos
- Métricas y gráficos
- Detección automática con IA (RegexCatalog ya preparado)
- Soporte para múltiples sujetos por documento

---

## 👨‍💻 Autor

Ruiz

---

## 💬 Nota

Este proyecto está diseñado siguiendo buenas prácticas profesionales:

- Arquitectura por capas con separación de responsabilidades
- Código documentado con XML comments
- Base de datos estructurada con stored procedures
- Sin almacenamiento de archivos en disco
- Preparado para escalar hacia detección con IA
