/*******************************************************************************
 Archivo    : Consultas.sql
 Descripción: Consultas de verificación y auditoría del sistema de
              anonimización documental. Permite monitorear el estado de las
              tablas, listar documentos procesados y revisar el detalle de
              cada anonimización realizada.
 Autor      : Ruiz
 Fecha      : 2026
 Uso        : Ejecutar en SQL Developer conectado como anonimizador@XEPDB1
*******************************************************************************/


-- =============================================
-- 1. CONTEO DE REGISTROS POR TABLA
-- =============================================
/*
 * Propósito  : Verifica la integridad del esquema mostrando cuántos registros
 *              existen en cada tabla del sistema. Útil para diagnóstico
 *              rápido y validación post-despliegue.
 *
 * Retorna    :
 *   Tabla       VARCHAR2   Nombre de la tabla consultada
 *   Registros   NUMBER     Cantidad de filas existentes en esa tabla
 *
 * Sin parámetros de entrada.
 */
SELECT 'PROCESS_STATUS'    AS Tabla, COUNT(*) AS Registros FROM PROCESS_STATUS   UNION ALL
SELECT 'ROLES',                       COUNT(*)              FROM ROLES             UNION ALL
SELECT 'USERS',                       COUNT(*)              FROM USERS             UNION ALL
SELECT 'DOCUMENTS',                   COUNT(*)              FROM DOCUMENTS         UNION ALL
SELECT 'DOCUMENT_VERSIONS',           COUNT(*)              FROM DOCUMENT_VERSIONS UNION ALL
SELECT 'ANONYMIZED_FIELDS',           COUNT(*)              FROM ANONYMIZED_FIELDS;


-- =============================================
-- 2. LISTADO DE DOCUMENTOS CON ID, HASH Y ESTADO
-- =============================================
/*
 * Propósito  : Lista todos los documentos registrados ordenados del más
 *              reciente al más antiguo. Usar esta consulta primero para
 *              obtener el DocumentId o FileHash que se necesita en las
 *              consultas 4 y 5.
 *
 * Retorna    :
 *   DocumentId        NUMBER     Identificador consecutivo del documento.
 *                                Recomendado para consultas manuales (Query 4)
 *   OriginalFileName  VARCHAR2   Nombre original del archivo subido
 *   FileHash          VARCHAR2   Hash SHA-256 del archivo. Útil para
 *                                búsquedas programáticas (Query 5)
 *   UploadedBy        VARCHAR2   Usuario que realizó la subida
 *   Fecha             VARCHAR2   Fecha y hora de creación (DD/MM/YYYY HH24:MI)
 *   Estado            VARCHAR2   Estado actual del proceso:
 *                                  UPLOADED    → Registrado, pendiente
 *                                  PROCESSING  → Anonimización en curso
 *                                  ANONYMIZED  → Completado exitosamente
 *                                  FAILED      → Error en el procesamiento
 *
 * Sin parámetros de entrada.
 */
SELECT
    d.DocumentId,
    d.OriginalFileName,
    d.FileHash,
    d.UploadedBy,
    TO_CHAR(d.CreatedAt, 'DD/MM/YYYY HH24:MI') AS Fecha,
    s.Name AS Estado
FROM DOCUMENTS d
INNER JOIN PROCESS_STATUS s ON d.CurrentStatusId = s.StatusId
ORDER BY d.CreatedAt DESC;


-- =============================================
-- 3. AUDITORÍA COMPLETA — TODOS LOS DOCUMENTOS
-- =============================================
/*
 * Propósito  : Genera un reporte de auditoría con todos los campos sensibles
 *              detectados y anonimizados en el sistema. Cumple con el
 *              registro de trazabilidad exigido por PRODHAB.
 *
 * Retorna    :
 *   DocumentId        NUMBER     Identificador del documento procesado
 *   OriginalFileName  VARCHAR2   Nombre original del archivo
 *   UploadedBy        VARCHAR2   Usuario que realizó la anonimización
 *   Fecha             VARCHAR2   Fecha y hora del proceso (DD/MM/YYYY HH24:MI)
 *   Campo             VARCHAR2   Tipo de dato sensible detectado.
 *                                  Ejemplos: NOMBRE, CEDULA, TELEFONO,
 *                                            EMAIL, CUENTA_BANCARIA
 *   ValorOriginal     VARCHAR2   Valor sensible encontrado en el documento
 *   Reemplazado       VARCHAR2   Etiqueta neutra asignada como sustituto.
 *                                  Ejemplos: [PERSONA_1], [CEDULA_1]
 *   MetodoDeteccion   VARCHAR2   Método usado: MANUAL o IA
 *
 * Sin parámetros de entrada.
 * Nota: Un documento con N campos anonimizados genera N filas en el resultado.
 */
SELECT
    d.DocumentId,
    d.OriginalFileName,
    d.UploadedBy,
    TO_CHAR(d.CreatedAt, 'DD/MM/YYYY HH24:MI') AS Fecha,
    f.FieldType        AS Campo,
    f.OriginalValue    AS ValorOriginal,
    f.AnonymizedValue  AS Reemplazado,
    f.DetectionMethod  AS MetodoDeteccion
FROM DOCUMENTS d
INNER JOIN DOCUMENT_VERSIONS v ON v.DocumentId = d.DocumentId
INNER JOIN ANONYMIZED_FIELDS f ON f.VersionId  = v.VersionId
ORDER BY d.CreatedAt DESC, f.FieldType;


-- =============================================
-- 4. AUDITORÍA POR DOCUMENTO — POR ID (recomendado para uso diario)
-- =============================================
/*
 * Propósito  : Detalla todos los campos anonimizados de un documento
 *              específico filtrando por su identificador consecutivo.
 *              Opción recomendada para consultas manuales: el ID es corto,
 *              legible y se obtiene fácilmente desde la Query 2.
 *
 * Cómo usarla:
 *   1. Ejecutar la Query 2 para ver los documentos disponibles
 *   2. Copiar el DocumentId del documento que se desea auditar
 *   3. Reemplazar el valor en la cláusula WHERE
 *
 * Parámetro de entrada:
 *   d.DocumentId   NUMBER   ID del documento a auditar (ver Query 2)
 *
 * Retorna    :
 *   DocumentId        NUMBER     ID del documento consultado
 *   OriginalFileName  VARCHAR2   Nombre del archivo auditado
 *   Campo             VARCHAR2   Tipo de dato sensible anonimizado
 *   ValorOriginal     VARCHAR2   Valor original detectado en el documento
 *   Reemplazado       VARCHAR2   Etiqueta neutra que lo sustituyó
 *   MetodoDeteccion   VARCHAR2   Método de detección: MANUAL o IA
 *   FechaDeteccion    VARCHAR2   Fecha y hora en que se registró el campo
 */
SELECT
    d.DocumentId,
    d.OriginalFileName,
    f.FieldType        AS Campo,
    f.OriginalValue    AS ValorOriginal,
    f.AnonymizedValue  AS Reemplazado,
    f.DetectionMethod  AS MetodoDeteccion,
    TO_CHAR(f.CreatedAt, 'DD/MM/YYYY HH24:MI') AS FechaDeteccion
FROM ANONYMIZED_FIELDS f
INNER JOIN DOCUMENT_VERSIONS v ON f.VersionId  = v.VersionId
INNER JOIN DOCUMENTS d         ON v.DocumentId = d.DocumentId
WHERE d.DocumentId = 8  -- ← modificar con el DocumentId deseado (ver Query 2)
ORDER BY f.FieldType;


-- =============================================
-- 5. AUDITORÍA POR DOCUMENTO — POR HASH (para uso programático / API)
-- =============================================
/*
 * Propósito  : Misma consulta que la Query 4 pero filtrando por el hash
 *              SHA-256 del archivo. Útil cuando se tiene acceso al archivo
 *              físico pero no al ID, o para verificar si un archivo ya fue
 *              procesado anteriormente (detección de duplicados).
 *              Para consultas manuales se recomienda usar la Query 4 (por ID).
 *
 * Cómo usarla:
 *   1. Ejecutar la Query 2 para obtener el FileHash del documento deseado
 *   2. Reemplazar el valor en la cláusula WHERE (hash de 64 caracteres)
 *
 * Parámetro de entrada:
 *   d.FileHash   VARCHAR2(64)   Hash SHA-256 del archivo original (ver Query 2)
 *
 * Retorna    :
 *   DocumentId        NUMBER     ID del documento que corresponde al hash
 *   OriginalFileName  VARCHAR2   Nombre del archivo auditado
 *   Campo             VARCHAR2   Tipo de dato sensible anonimizado
 *   ValorOriginal     VARCHAR2   Valor original detectado en el documento
 *   Reemplazado       VARCHAR2   Etiqueta neutra que lo sustituyó
 *   MetodoDeteccion   VARCHAR2   Método de detección: MANUAL o IA
 *   FechaDeteccion    VARCHAR2   Fecha y hora en que se registró el campo
 */
SELECT
    d.DocumentId,
    d.OriginalFileName,
    f.FieldType        AS Campo,
    f.OriginalValue    AS ValorOriginal,
    f.AnonymizedValue  AS Reemplazado,
    f.DetectionMethod  AS MetodoDeteccion,
    TO_CHAR(f.CreatedAt, 'DD/MM/YYYY HH24:MI') AS FechaDeteccion
FROM ANONYMIZED_FIELDS f
INNER JOIN DOCUMENT_VERSIONS v ON f.VersionId  = v.VersionId
INNER JOIN DOCUMENTS d         ON v.DocumentId = d.DocumentId
WHERE d.FileHash = 'reemplazar_con_hash_del_archivo'  -- ← hash SHA-256 de 64 caracteres (ver Query 2)
ORDER BY f.FieldType;