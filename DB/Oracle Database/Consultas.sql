/*******************************************************************************
 Archivo    : Consultas.sql
 Descripción: Consultas de verificación y auditoría del sistema
 Autor      : Ruiz
 Fecha      : 2026
*******************************************************************************/

-- =============================================
-- 1. CONTEO DE REGISTROS POR TABLA
-- =============================================
SELECT 'PROCESS_STATUS'   AS Tabla, COUNT(*) AS Registros FROM PROCESS_STATUS  UNION ALL
SELECT 'ROLES',                      COUNT(*)              FROM ROLES            UNION ALL
SELECT 'USERS',                      COUNT(*)              FROM USERS            UNION ALL
SELECT 'DOCUMENTS',                  COUNT(*)              FROM DOCUMENTS        UNION ALL
SELECT 'DOCUMENT_VERSIONS',          COUNT(*)              FROM DOCUMENT_VERSIONS UNION ALL
SELECT 'ANONYMIZED_FIELDS',          COUNT(*)              FROM ANONYMIZED_FIELDS;

-- =============================================
-- 2. LISTADO DE DOCUMENTOS CON SU ID Y ESTADO
--    Ejecutar para ver los DocumentId disponibles
-- =============================================
SELECT
    d.DocumentId,
    d.OriginalFileName,
    d.UploadedBy,
    TO_CHAR(d.CreatedAt, 'DD/MM/YYYY HH24:MI') AS Fecha,
    s.Name AS Estado
FROM DOCUMENTS d
INNER JOIN PROCESS_STATUS s ON d.CurrentStatusId = s.StatusId
ORDER BY d.CreatedAt DESC;

-- =============================================
-- 3. AUDITORÍA COMPLETA — TODOS LOS DOCUMENTOS
--    Muestra qué se anonimizó en cada documento
-- =============================================
SELECT
    d.DocumentId,
    d.OriginalFileName,
    d.UploadedBy,
    TO_CHAR(d.CreatedAt, 'DD/MM/YYYY HH24:MI') AS Fecha,
    f.FieldType        AS Campo,
    f.OriginalValue    AS ValorOriginal,
    f.AnonymizedValue  AS Reemplazado
FROM DOCUMENTS d
INNER JOIN DOCUMENT_VERSIONS v ON v.DocumentId = d.DocumentId
INNER JOIN ANONYMIZED_FIELDS f ON f.VersionId  = v.VersionId
ORDER BY d.CreatedAt DESC, f.FieldType;

-- =============================================
-- 4. AUDITORÍA POR DOCUMENTO
-- =============================================
SELECT
    d.DocumentId,
    d.OriginalFileName,
    f.FieldType        AS Campo,
    f.OriginalValue    AS ValorOriginal,
    f.AnonymizedValue  AS Reemplazado
FROM ANONYMIZED_FIELDS f
INNER JOIN DOCUMENT_VERSIONS v ON f.VersionId  = v.VersionId
INNER JOIN DOCUMENTS d         ON v.DocumentId = d.DocumentId
WHERE d.DocumentId = 8  -- ← cambiá este número
ORDER BY f.FieldType;