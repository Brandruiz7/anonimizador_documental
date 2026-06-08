/******************************************************************************
 Archivo    : 01_anonimizador_config.sql
 Descripción: Tabla de configuración centralizada para el sistema Anónima CGR.
              Reemplaza las constantes hardcodeadas en los packages PL/SQL.
 Autor      : Ruiz
 Esquema    : ANONIMIZADOR @ XEPDB1

 INSTRUCCIONES:
   1. Ejecutar como ANONIMIZADOR en XEPDB1
   2. La tabla se crea con IF NOT EXISTS (bloque BEGIN/EXCEPTION)
   3. Los INSERT usan MERGE para ser idempotentes (se puede re-ejecutar)
******************************************************************************/

-- =============================================
-- TABLA
-- =============================================
BEGIN
    EXECUTE IMMEDIATE '
        CREATE TABLE anonimizador_config (
            config_key   VARCHAR2(100)  NOT NULL,
            config_value VARCHAR2(500)  NOT NULL,
            description  VARCHAR2(500),
            updated_at   TIMESTAMP      DEFAULT SYSTIMESTAMP,
            CONSTRAINT pk_anon_config PRIMARY KEY (config_key)
        )';
    DBMS_OUTPUT.PUT_LINE('Tabla anonimizador_config creada.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -955 THEN
            DBMS_OUTPUT.PUT_LINE('Tabla anonimizador_config ya existe — omitida.');
        ELSE
            RAISE;
        END IF;
END;
/

-- =============================================
-- VALORES — MERGE para idempotencia
-- Se puede re-ejecutar sin duplicar filas.
-- Para cambiar un valor: actualizar config_value
-- en este script o hacer UPDATE directo.
-- =============================================
MERGE INTO anonimizador_config t
USING (
    SELECT 'API_BASE_URL' AS config_key,
           'http://127.0.0.1:5255' AS config_value,
           'URL base de la API .NET. Cambiar a la URL de GCP en producción.' AS description
    FROM dual
    UNION ALL
    SELECT 'SESSION_RETENTION_H',
           '2',
           'Horas de retención de archivos temporales en wizard_session_files y wizard_result_files.'
    FROM dual
) s
ON (t.config_key = s.config_key)
WHEN MATCHED THEN
    UPDATE SET t.config_value = s.config_value,
               t.description  = s.description,
               t.updated_at   = SYSTIMESTAMP
WHEN NOT MATCHED THEN
    INSERT (config_key, config_value, description, updated_at)
    VALUES (s.config_key, s.config_value, s.description, SYSTIMESTAMP);

COMMIT;

-- =============================================
-- VERIFICAR
-- =============================================
SELECT config_key, config_value, description, updated_at
FROM   anonimizador_config
ORDER BY config_key;