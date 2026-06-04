# 📄 Anonimizador Documental — CGR

Sistema de anonimización de documentos jurídicos desarrollado para la **Contraloría General de la República de Costa Rica**. Reemplaza datos sensibles y personales por etiquetas neutrales, siguiendo la normativa **PRODHAB** sobre protección de datos personales.

---

## 📑 Índice

- [Descripción](#-descripción)
- [Funcionalidades](#-funcionalidades-principales)
- [Arquitectura](#-arquitectura)
- [Tecnologías](#-tecnologías)
- [Estructura del proyecto](#-estructura-del-proyecto)
- [Instalación rápida](#-instalación-rápida)
- [Documentación detallada](#-documentación-detallada)
- [Autor](#-autor)

---

## 📌 Descripción

El sistema expone una **API REST** desarrollada en **.NET 8** que permite:

- Anonimizar documentos `.docx` y `.pdf` reemplazando datos sensibles por etiquetas neutrales
- Detectar datos sensibles de forma **manual** o mediante **IA semántica** (Ollama/Mistral o Gemini)
- Registrar auditoría granular campo por campo de cada anonimización
- Exponer métricas e historial para monitoreo institucional

El frontend institucional se implementa en **Oracle APEX 26.1**, consumiendo esta API mediante requests REST server-side con autenticación JWT.

---

## ✨ Funcionalidades principales

- 🔐 Autenticación JWT con roles (Admin / Operator)
- 📄 Anonimización de documentos `.docx` y `.pdf`
- 🤖 Detección híbrida: **Regex preciso + IA semántica** (Ollama/Mistral o Gemini)
- 👥 Soporte para múltiples personas por documento con variantes de nombre, cédula, teléfono, cuenta bancaria y condición médica
- 🏛️ Campos PRODHAB: datos personales y datos sensibles
- 📊 Historial de documentos y métricas para el dashboard
- 🔍 Auditoría granular por campo anonimizado con hash SHA256
- 🛡️ Headers de seguridad y rate limiting por IP

> ⚠️ **CORS deshabilitado por diseño:** el browser nunca llama directamente a la API. Todos los requests van server-side desde APEX vía `UTL_HTTP` en el package `PKG_WIZARD_ANON`.

---

## 🧠 Arquitectura

```text
Oracle APEX 26.1 (browser)
    ↓  submit de página
Oracle APEX (servidor) — PKG_WIZARD_ANON
    ↓  UTL_HTTP + JWT  (server-to-server)
API REST .NET 8 — Controllers
    ↓
CrossCutting — CorrelationIdMiddleware, ExceptionMiddleware
    ↓
Application — Services, DTOs
    ↓
Interfaces — Contratos (IDocumentService, IAuthService, etc.)
    ↓
Infrastructure — Repositories, DbConnectionFactory
    ↓
Oracle XE 21c — Stored Procedures
```

### Flujo de anonimización (modo manual)

```text
1. Usuario sube archivo en Paso 1 → APEX lo guarda en wizard_session_files
2. Usuario ingresa personas y variantes en Paso 2
3. PKG_WIZARD_ANON.upload_document lee el archivo desde wizard_session_files
4. Construye multipart/form-data y llama a POST /api/documents/upload vía UTL_HTTP
5. API valida JWT, anonimiza en memoria y retorna el documento como stream binario
6. Package guarda el resultado en wizard_result_files con clave de sesión
7. Paso 3 muestra preview del PDF y permite descargar vía Application Process
```

> ⚠️ El documento nunca se escribe a disco — todo el procesamiento ocurre en RAM.

---

## 🧩 Tecnologías

| Componente | Tecnología |
|---|---|
| API | .NET 8, ASP.NET Core Web API |
| Frontend institucional | Oracle APEX 26.1 |
| Base de datos | Oracle XE 21c (Stored Procedures) |
| ORM | Dapper + OracleDynamicParameters |
| Documentos Word | OpenXML SDK |
| Documentos PDF | PdfPig, PdfSharp, PDFtoImage, SkiaSharp |
| IA local | Ollama (Mistral) |
| IA en la nube | Google Gemini API |
| Autenticación | JWT Bearer (HMAC-SHA256) |
| Passwords | BCrypt.Net (cost factor 12) |
| Logs | Serilog (consola + archivo rotativo) |
| Documentación API | Swagger / Swashbuckle |

---

## 📁 Estructura del proyecto

```text
/
├── Anonimizador - API/
│   ├── API/Controllers/
│   │   ├── AuthController.cs          ← login, generación de JWT y generate-hash
│   │   └── DocumentsController.cs     ← upload, análisis, historial, métricas
│   ├── Application/
│   │   ├── Common/
│   │   │   ├── RegexCatalog.cs        ← expresiones regulares compiladas
│   │   │   ├── TextAnonymizationEngine.cs ← motor de reemplazo de texto
│   │   │   ├── PdfLineInfo.cs         ← modelo de línea PDF
│   │   │   ├── PdfRedactionInfo.cs    ← modelo de redacción PDF
│   │   │   └── PdfWordInfo.cs         ← modelo de palabra PDF
│   │   ├── DTOs/                      ← objetos de transferencia de datos
│   │   └── Services/
│   │       ├── Analysis/
│   │       │   ├── DocumentAnalysisService.cs ← orquesta Regex + IA
│   │       │   ├── OllamaService.cs   ← cliente IA local (activo)
│   │       │   └── GeminiService.cs   ← cliente IA nube (comentado)
│   │       ├── Auth/AuthService.cs    ← validación BCrypt + generación JWT
│   │       ├── Documents/DocumentService.cs ← orquesta el flujo completo
│   │       └── Processors/
│   │           ├── WordDocumentProcessor.cs ← anonimización DOCX
│   │           └── PdfDocumentProcessor.cs  ← anonimización PDF por imagen
│   ├── CrossCutting/
│   │   ├── CorrelationIdMiddleware.cs ← asigna X-Correlation-ID a cada request
│   │   └── ExceptionMiddleware.cs     ← captura excepciones y retorna JSON
│   ├── Infrastructure/
│   │   ├── Data/
│   │   │   ├── DbConnectionFactory.cs       ← fábrica de conexiones Oracle
│   │   │   └── OracleDynamicParameters.cs   ← parámetros tipados para Dapper
│   │   └── Repositories/
│   │       ├── DocumentRepository.cs  ← acceso a datos de documentos
│   │       └── UserRepository.cs      ← acceso a datos de usuarios
│   └── Interfaces/                    ← contratos de servicios y repositorios
│
└── DB/
    ├── Oracle Database/
    │   ├── AnonimizadorDB.sql         ← script base Oracle XE 21c (incluye wizard_session_files)
    │   ├── pkg_wizard_anon.sql        ← package PL/SQL del wizard de anonimización
    │   ├── Package.sql                ← package PL/SQL de autenticación APEX
    │   └── Consultas.sql              ← consultas de auditoría y verificación
    └── Sql Server/
        ├── DocumentAnonymizerDB.sql   ← script base SQL Server (referencia)
        ├── Files/
        │   └── SqlServer_CSharp_Files.zip ← archivos C# para SQL Server
        └── Migrations/
            ├── 001_AddFullNameToUsers.sql
            └── 002_ExpandAnonymizationFields.sql
```

---

## 🚀 Instalación rápida

### Requisitos

- .NET 8 SDK
- Oracle XE 21c (o SQL Server — ver [documentación de BD](docs/base-de-datos.md))
- Ollama con Mistral instalado (o API Key de Gemini — ver [configuración de IA](docs/configuracion.md))
- Oracle APEX 26.1 con ORDS (para el frontend institucional)

### Pasos — API

```bash
# 1. Clonar el repositorio
git clone https://github.com/Brandruiz7/anonimizador_documental

# 2. Configurar appsettings
# Copiar appsettings.example.json → appsettings.json y completar los valores
# Nota: la sección Cors está comentada — no se necesita en esta arquitectura

# 3. Ejecutar script de BD en SQL Developer (conectado como anonimizador@XEPDB1)
# DB/Oracle Database/AnonimizadorDB.sql

# 4. Ejecutar el package del wizard en SQL Developer
# DB/Oracle Database/pkg_wizard_anon.sql

# 5. Iniciar Ollama
ollama serve
ollama pull mistral

# 6. Correr la API
cd "Anonimizador - API"
dotnet run
```

Swagger disponible en `https://localhost:7108/swagger` (solo en Development).

### Pasos — Oracle APEX

```bash
# 1. Arrancar Oracle XE y ORDS (ver docs/oracle-apex.md para el detalle)
net start OracleServiceXE
net start OracleXETNSListener
# java -jar ords.war serve

# 2. Configurar el ACL de red como SYS en XEPDB1
# DB/Oracle Database/Package.sql — sección ACL

# 3. Ejecutar el package de autenticación como anonimizador@XEPDB1
# DB/Oracle Database/Package.sql

# 4. Ejecutar el package del wizard como anonimizador@XEPDB1
# DB/Oracle Database/pkg_wizard_anon.sql

# 5. Crear la app en App Builder con tema VITA CGR y Authentication Scheme custom
# Ver guía completa: docs/oracle-apex.md
```

### Usuario administrador por defecto

```
Usuario:    admin
Contraseña: Admin123!
```

Generar el hash de una contraseña nueva con: `GET /api/auth/generate-hash?password=TuPassword`

---

## 📚 Documentación detallada

| Documento | Descripción |
|---|---|
| [Oracle APEX](docs/oracle-apex.md) | Configuración del frontend institucional: wizard manual completo, packages PL/SQL, Application Processes y consideraciones de producción |
| [Anonimización manual](docs/anonimizacion-manual.md) | Flujo paso a paso del modo manual — cómo se construyen los targets y se aplican los reemplazos |
| [Anonimización con IA](docs/anonimizacion-ia.md) | Detección híbrida Regex + Ollama/Gemini, prompt estructurado y parseo de respuesta |
| [Procesamiento PDF](docs/procesamiento-pdf.md) | Pipeline PDF→imagen→redacción por píxeles→PDF, coordenadas y etiquetas en rectángulos |
| [Procesamiento DOCX](docs/procesamiento-docx.md) | Zonas cubiertas, OpenXML SDK y motor de reemplazo de texto |
| [Base de datos](docs/base-de-datos.md) | Tablas, Stored Procedures, Oracle vs SQL Server, hash SHA256 y consultas de auditoría |
| [Seguridad](docs/seguridad.md) | JWT, BCrypt, rate limiting y headers de seguridad |
| [Configuración](docs/configuracion.md) | appsettings, variables de entorno, motores de IA y cadenas de conexión |

---

## 👨‍💻 Autor

Ruiz — Ingeniero en Sistemas