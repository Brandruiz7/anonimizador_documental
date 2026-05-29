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

El frontend institucional se implementa en **Oracle APEX**, consumiendo esta API mediante requests REST con autenticación JWT.

---

## ✨ Funcionalidades principales

- 🔐 Autenticación JWT con roles (Admin / Operator)
- 📄 Anonimización de documentos `.docx` y `.pdf`
- 🤖 Detección híbrida: **Regex preciso + IA semántica** (Ollama/Mistral o Gemini)
- 👥 Soporte para múltiples personas por documento con variaciones de nombre, cédula y teléfono
- 🏛️ Campos PRODHAB: datos personales y datos sensibles (cuenta bancaria, condición médica)
- 📊 Historial de documentos y métricas para el dashboard
- 🔍 Auditoría granular por campo anonimizado con hash SHA256
- 🛡️ Headers de seguridad, rate limiting por IP y CORS configurable

---

## 🧠 Arquitectura

```text
Cliente (Oracle APEX / Bruno / Swagger)
    ↓  HTTPS + JWT
API REST — Controllers
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

### Flujo de anonimización

```text
1. Cliente envía documento + datos de personas → POST /api/documents/upload
2. API valida JWT, rol y archivo
3. Calcula hash SHA256 del original
4. Registra proceso en BD (estado: PROCESSING)
5. Anonimiza en memoria según el formato:
   ├── DOCX → reemplazo en párrafos, tablas, headers, footers, textboxes
   └── PDF  → renderizado a imagen → redacción por píxeles → reconstrucción
6. Registra versión ANONYMIZED con hash en BD
7. Registra auditoría campo por campo
8. Actualiza estado a ANONYMIZED
9. Retorna documento como stream para descarga
```

> ⚠️ El documento nunca se escribe a disco — todo el procesamiento ocurre en RAM.

---

## 🧩 Tecnologías

| Componente | Tecnología |
|---|---|
| API | .NET 8, ASP.NET Core Web API |
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
│   │   ├── AuthController.cs          ← login y generación de JWT
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
    │   ├── AnonimizadorDB.sql         ← script base Oracle XE 21c
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

### Pasos

```bash
# 1. Clonar el repositorio
git clone https://github.com/Brandruiz7/anonimizador_documental

# 2. Configurar appsettings
# Copiar appsettings.example.json → appsettings.json y completar los valores

# 3. Ejecutar script de BD en SQL Developer (conectado como anonimizador@XEPDB1)
# DB/Oracle Database/AnonimizadorDB.sql

# 4. Iniciar Ollama
ollama serve
ollama pull mistral

# 5. Correr la API
cd "Anonimizador - API"
dotnet run
```

### Usuario administrador por defecto

```
Usuario:    admin
Contraseña: Admin123!
```

> ⚠️ Cambiar en producción generando un nuevo hash con:
> `GET /api/auth/generate-hash?password=TuPassword`

---

## 📚 Documentación detallada

| Documento | Descripción |
|---|---|
| [Anonimización manual](docs/anonimizacion-manual.md) | Flujo paso a paso del modo manual — cómo se construyen los targets y se aplican los reemplazos |
| [Anonimización con IA](docs/anonimizacion-ia.md) | Detección híbrida Regex + Ollama/Gemini, prompt estructurado y parseo de respuesta |
| [Procesamiento PDF](docs/procesamiento-pdf.md) | Pipeline PDF→imagen→redacción por píxeles→PDF, coordenadas y etiquetas en rectángulos |
| [Procesamiento DOCX](docs/procesamiento-docx.md) | Zonas cubiertas, OpenXML SDK y motor de reemplazo de texto |
| [Base de datos](docs/base-de-datos.md) | Tablas, Stored Procedures, Oracle vs SQL Server, hash SHA256 y consultas de auditoría |
| [Seguridad](docs/seguridad.md) | JWT, BCrypt, rate limiting, headers de seguridad y CORS |
| [Configuración](docs/configuracion.md) | appsettings, variables de entorno, motores de IA y cadenas de conexión |

---

## 👨‍💻 Autor

Ruiz — Ingeniero en Sistemas