# 🗄️ Base de datos

[← Volver al README principal](../README.md)

## Motor soportado

El sistema soporta **Oracle XE 21c** (recomendado para despliegue institucional) y **SQL Server** (para desarrollo o entornos Microsoft).

---

## Tablas

### Tablas del dominio principal

| Tabla | Descripción |
|---|---|
| `DOCUMENTS` | Metadata de cada documento procesado |
| `DOCUMENT_VERSIONS` | Versiones del documento (ANONYMIZED) con hash SHA256 |
| `ANONYMIZED_FIELDS` | Auditoría campo por campo de cada reemplazo |
| `PROCESS_STATUS` | Catálogo de estados del proceso |
| `PROCESS_ERRORS` | Registro centralizado de errores |
| `USERS` | Usuarios del sistema |
| `ROLES` | Roles (Admin, Operator) |

### Tablas temporales del wizard APEX

| Tabla | TTL | Descripción |
|---|---|---|
| `wizard_session_files` | 2 horas | Persiste el archivo original entre pasos del wizard. APEX elimina `apex_application_temp_files` al terminar cada request, por lo que el archivo se copia aquí al pasar del Paso 1 al Paso 2. Clave: `WIZ_FILE_{APP_SESSION}` |
| `wizard_result_files` | 2 horas | Almacena el documento anonimizado (BLOB) entre el procesamiento y la descarga del usuario. Clave: `WIZ_{APP_SESSION}_{TIMESTAMP}` |

Ambas tablas se limpian automáticamente al inicio de cada anonimización mediante `PKG_WIZARD_ANON.clean_old_results`.

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

## Package PL/SQL del wizard — `PKG_WIZARD_ANON`

Ver `DB/Oracle Database/pkg_wizard_anon.sql` para el código completo.

### Objetos que crea

```sql
-- Tablas temporales (creadas con EXECUTE IMMEDIATE si no existen)
wizard_result_files   -- resultado del documento anonimizado
wizard_session_files  -- archivo original persistido entre pasos

-- Tipos
t_person_data    -- OBJECT con 15 campos (datos + variantes)
t_persons_list   -- TABLE OF t_person_data

-- Package
pkg_wizard_anon  -- spec + body
```

### Procedimientos y funciones públicos

| Nombre | Tipo | Descripción |
|---|---|---|
| `upload_document` | PROCEDURE | Llama a `POST /api/documents/upload`. Lee el archivo de `wizard_session_files` usando `p_session_key`. Guarda el resultado en `wizard_result_files`. |
| `analyze_document` | FUNCTION | Llama a `POST /api/documents/analyze`. Retorna JSON con personas detectadas por la IA. |
| `get_result_blob` | PROCEDURE | Recupera el BLOB del documento anonimizado por clave. Usado por los Application Processes de descarga. |
| `clean_old_results` | PROCEDURE | Elimina registros de más de 2 horas en `wizard_result_files` y `wizard_session_files`. |

### Firma de `upload_document`

```sql
PROCEDURE upload_document(
    p_file_name     IN  VARCHAR2,   -- nombre en wizard_session_files
    p_jwt_token     IN  VARCHAR2,   -- token JWT Bearer
    p_case_number   IN  VARCHAR2 DEFAULT NULL,
    p_office_number IN  VARCHAR2 DEFAULT NULL,
    p_persons       IN  t_persons_list,
    p_session_key   IN  VARCHAR2,   -- clave en wizard_session_files
    p_result_key    OUT VARCHAR2,   -- clave para recuperar el resultado
    p_result_file   OUT VARCHAR2    -- nombre del archivo anonimizado
);
```

### Variantes de campos

Las variantes permiten que el sistema anonimice todas las formas en que puede aparecer un dato. Se almacenan en las columnas c011-c015 de `APEX_COLLECTION('WIZARD_PERSONS')` separadas por `|` y se envían al API como arrays indexados:

```
Persons[0].NameVariations[0] = "Brandon"
Persons[0].NameVariations[1] = "Ruiz Miranda"
Persons[0].IdVariations[0]   = "123456789"
Persons[0].PhoneVariations[0] = "22222222"
Persons[0].BankAccountVariations[0] = "CR21..."
Persons[0].MedicalVariations[0] = "Miopía severa"
```

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
- Auditoría por hash SHA256

---

## Ver también

- [Seguridad](seguridad.md)
- [Configuración](configuracion.md)