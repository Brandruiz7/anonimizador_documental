/******************************************************************************
 Migración   : 003_FixMetricsSPs
 Descripción : Corrige SP_METRICS_SUMMARY y SP_METRICS_DOCUMENTS_BY_MONTH
               para sincronizarlos con el DTO de la API .NET y el dashboard
               de Oracle APEX.

               SP_METRICS_SUMMARY agrega:
                 - ThisMonth   → documentos creados en el mes actual
                 - ActiveUsers → usuarios distintos que han subido documentos
               y mantiene TotalDocuments, TotalAnonymized, TotalFailed, TotalSizeKB.

               SP_METRICS_DOCUMENTS_BY_MONTH agrega:
                 - Límite a los últimos 6 meses (igual que SQL Server)
                 - MonthNumber → número de mes (1-12) para ordenar en el chart
                 - Year        → año para ordenar en el chart
               y cambia el formato de Month a 'Mon YYYY' en español
               para que el label del eje X sea legible.

 Autor       : Ruiz
 Esquema     : ANONIMIZADOR @ XEPDB1
 Integrado   : ⬜ Pendiente
******************************************************************************/

-- =============================================
-- SP_METRICS_SUMMARY
-- =============================================
/*
 * Retorna los indicadores globales del sistema en una sola fila.
 *
 * OUT p_ResultSet  SYS_REFCURSOR  Una fila con:
 *   TotalDocuments  NUMBER  Total de documentos registrados
 *   TotalAnonymized NUMBER  Documentos con StatusId = 3 (ANONYMIZED)
 *   TotalFailed     NUMBER  Documentos con StatusId = 4 (FAILED)
 *   TotalSizeKB     NUMBER  Suma del tamaño en KB de todos los documentos
 *   ThisMonth       NUMBER  Documentos creados en el mes y año actuales
 *   ActiveUsers     NUMBER  Usuarios distintos que han subido documentos
 */
CREATE OR REPLACE PROCEDURE SP_METRICS_SUMMARY (
    p_ResultSet OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            COUNT(*)                                                    AS TotalDocuments,
            SUM(CASE WHEN CurrentStatusId = 3 THEN 1 ELSE 0 END)       AS TotalAnonymized,
            SUM(CASE WHEN CurrentStatusId = 4 THEN 1 ELSE 0 END)       AS TotalFailed,
            COALESCE(SUM(FileSizeKB), 0)                                AS TotalSizeKB,
            SUM(CASE
                WHEN EXTRACT(MONTH FROM CreatedAt) = EXTRACT(MONTH FROM SYSDATE)
                 AND EXTRACT(YEAR  FROM CreatedAt) = EXTRACT(YEAR  FROM SYSDATE)
                THEN 1 ELSE 0
            END)                                                        AS ThisMonth,
            COUNT(DISTINCT UploadedBy)                                  AS ActiveUsers
        FROM DOCUMENTS;
END;
/

-- =============================================
-- SP_METRICS_DOCUMENTS_BY_MONTH
-- =============================================
/*
 * Retorna el conteo de documentos de los últimos 6 meses completos
 * más el mes actual, ordenado cronológicamente (más antiguo primero)
 * para que el gráfico de barras/línea se renderice correctamente.
 *
 * OUT p_ResultSet  SYS_REFCURSOR  Una fila por mes con actividad:
 *   Month        VARCHAR2  Período legible. Ej: 'Ene 2026'
 *   MonthNumber  NUMBER    Número de mes (1-12) — para ordenar en el chart
 *   Year         NUMBER    Año — para ordenar en el chart
 *   Total        NUMBER    Documentos creados en ese mes
 *
 * Nota: TO_CHAR con 'Mon' en Oracle usa NLS_DATE_LANGUAGE.
 * Se fuerza 'SPANISH' para consistencia independiente del NLS del servidor.
 */
CREATE OR REPLACE PROCEDURE SP_METRICS_DOCUMENTS_BY_MONTH (
    p_ResultSet OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            TO_CHAR(TRUNC(CreatedAt, 'MM'), 'Mon YYYY', 'NLS_DATE_LANGUAGE=SPANISH') AS Month,
            EXTRACT(MONTH FROM CreatedAt)                                              AS MonthNumber,
            EXTRACT(YEAR  FROM CreatedAt)                                              AS Year,
            COUNT(*)                                                                   AS Total
        FROM DOCUMENTS
        WHERE CreatedAt >= ADD_MONTHS(TRUNC(SYSDATE, 'MM'), -5)
        GROUP BY
            TRUNC(CreatedAt, 'MM'),
            EXTRACT(MONTH FROM CreatedAt),
            EXTRACT(YEAR  FROM CreatedAt)
        ORDER BY
            EXTRACT(YEAR  FROM CreatedAt),
            EXTRACT(MONTH FROM CreatedAt);
END;
/

-- =============================================
-- VERIFICAR
-- =============================================
SELECT object_name, object_type, status
FROM   user_objects
WHERE  object_name IN (
    'SP_METRICS_SUMMARY',
    'SP_METRICS_DOCUMENTS_BY_MONTH'
)
ORDER BY object_name;

-- =============================================
-- TEST RÁPIDO (opcional, ejecutar por separado)
-- =============================================
/*
SET SERVEROUTPUT ON

-- Test SP_METRICS_SUMMARY
DECLARE
    l_cur SYS_REFCURSOR;
    l_total_docs   NUMBER;
    l_anonymized   NUMBER;
    l_failed       NUMBER;
    l_size_kb      NUMBER;
    l_this_month   NUMBER;
    l_active_users NUMBER;
BEGIN
    SP_METRICS_SUMMARY(l_cur);
    FETCH l_cur INTO l_total_docs, l_anonymized, l_failed,
                     l_size_kb, l_this_month, l_active_users;
    CLOSE l_cur;
    DBMS_OUTPUT.PUT_LINE('TotalDocuments : ' || l_total_docs);
    DBMS_OUTPUT.PUT_LINE('TotalAnonymized: ' || l_anonymized);
    DBMS_OUTPUT.PUT_LINE('TotalFailed    : ' || l_failed);
    DBMS_OUTPUT.PUT_LINE('TotalSizeKB    : ' || l_size_kb);
    DBMS_OUTPUT.PUT_LINE('ThisMonth      : ' || l_this_month);
    DBMS_OUTPUT.PUT_LINE('ActiveUsers    : ' || l_active_users);
END;
/

-- Test SP_METRICS_DOCUMENTS_BY_MONTH
DECLARE
    l_cur         SYS_REFCURSOR;
    l_month       VARCHAR2(20);
    l_month_num   NUMBER;
    l_year        NUMBER;
    l_total       NUMBER;
BEGIN
    SP_METRICS_DOCUMENTS_BY_MONTH(l_cur);
    LOOP
        FETCH l_cur INTO l_month, l_month_num, l_year, l_total;
        EXIT WHEN l_cur%NOTFOUND;
        DBMS_OUTPUT.PUT_LINE(l_month || ' (' || l_year || '-' || l_month_num || '): ' || l_total);
    END LOOP;
    CLOSE l_cur;
END;
/
*/