# 🗄️ Base de datos

[← Volver al README principal](../README.md)

## Motor soportado

El sistema soporta **Oracle XE 21c** (recomendado para despliegue institucional) y **SQL Server** (para desarrollo o entornos Microsoft).

---

## Tablas

| Tabla | Descripción |
|---|---|
| `DOCUMENTS` | Metadata de cada documento procesado |
| `DOCUMENT_VERSIONS` | Versiones del documento (ANONYMIZED) con hash SHA256 |
| `ANONYMIZED_FIELDS` | Auditoría campo por campo de cada reemplazo |
| `PROCESS_STATUS` | Catálogo de estados del proceso |
| `PROCESS_ERRORS` | Registro centralizado de errores |
| `USERS` | Usuarios del sistema |
| `ROLES` | Roles (Admin, Operator) |

---

## Estados del proceso

| ID | Nombre | Descripción |
|---|---|---|
| 1 | UPLOADED | Documento registrado, pendiente de procesar |
| 2 | PROCESSING | Proceso en curso |
| 3 | ANONYMIZED | Anonimización completada exitosamente |
| 4 | FAILED | Error durante el procesamiento |

---

## Hash SHA256

Cada documento genera dos hashes:

- **Hash original** (`DOCUMENTS.FileHash`): calculado sobre el archivo antes de anonimizar
- **Hash anonimizado** (`DOCUMENT_VERSIONS.FileHash`): calculado sobre el resultado

**Utilidades del hash:**
- Verificar integridad del documento — detecta alteraciones post-anonimización
- Detectar documentos duplicados antes de procesar
- Trazabilidad legal — prueba de qué se anonimizó y cuándo
- Base para un portal de consulta — el usuario ingresa el hash y ve el reporte de auditoría

---

## Stored Procedures

| SP | Descripción |
|---|---|
| `SP_DOCUMENT_PROCESS_INSERT` | Registra nuevo proceso, retorna DocumentId |
| `SP_DOCUMENT_VERSION_INSERT` | Registra versión anonimizada, retorna VersionId |
| `SP_ANONYMIZED_FIELD_INSERT` | Registra un campo anonimizado para auditoría |
| `SP_DOCUMENT_PROCESS_UPDATE_STATUS` | Actualiza el estado del proceso |
| `SP_DOCUMENT_GET_ALL` | Retorna historial para el dashboard |
| `SP_DOCUMENT_GET_FULL` | Retorna documento completo con versiones y campos |
| `SP_DOCUMENT_GET_BY_HASH` | Busca un documento por hash SHA256 |
| `SP_USER_GET_BY_USERNAME` | Busca usuario para autenticación |
| `SP_METRICS_SUMMARY` | Resumen general para tarjetas del dashboard |
| `SP_METRICS_DOCUMENTS_BY_MONTH` | Documentos por mes para gráfico de línea |
| `SP_METRICS_DOCUMENTS_BY_STATUS` | Documentos por estado para gráfico de dona |
| `SP_METRICS_DOCUMENTS_BY_USER` | Documentos por usuario para gráfico de dona |

---

## Oracle vs SQL Server

| Concepto | SQL Server | Oracle XE |
|---|---|---|
| Contenedor | Base de datos (`DocumentAnonymizerDB`) | Esquema (`anonimizador@XEPDB1`) |
| Auto-increment | `IDENTITY(1,1)` | Secuencias + Triggers |
| Fecha actual | `SYSDATETIME()` | `SYSTIMESTAMP` |
| Texto largo | `NVARCHAR(MAX)` | `CLOB` |
| Retorno de filas en SP | `SELECT` directo | `SYS_REFCURSOR` OUT |
| Parámetros OUT | `ExecuteScalarAsync` Dapper | `OracleDynamicParameters` |
| Script BD | `DB/Sql Server/DocumentAnonymizerDB.sql` | `DB/Oracle Database/AnonimizadorDB.sql` |

---

## Archivos a modificar al cambiar de motor

| Archivo | Cambio requerido |
|---|---|
| `appsettings.json` | Cadena de conexión |
| `Infrastructure/Data/DbConnectionFactory.cs` | `SqlConnection` ↔ `OracleConnection` |
| `Infrastructure/Repositories/DocumentRepository.cs` | Objetos anónimos ↔ `OracleDynamicParameters` |
| `Infrastructure/Repositories/UserRepository.cs` | Objetos anónimos ↔ `OracleDynamicParameters` |

> Los archivos C# originales para SQL Server están en `DB/Sql Server/Files/SqlServer_CSharp_Files.zip`.

---

## Consultas de auditoría

Ver `DB/Oracle Database/Consultas.sql` para:
- Conteo de registros por tabla
- Listado de documentos con ID y estado
- Auditoría completa de todos los documentos
- Auditoría por DocumentId específico

---

## Ver también

- [Seguridad](seguridad.md)
- [Configuración](configuracion.md)