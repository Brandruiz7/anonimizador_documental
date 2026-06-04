/******************************************************************************
 Archivo    : pkg_wizard_anon.sql
 Descripción: Package PL/SQL para el Wizard de Anonimización en Oracle APEX.
 Autor      : Ruiz
 Esquema    : ANONIMIZADOR @ XEPDB1

 INSTRUCCIONES:
   1. Ejecutar como ANONIMIZADOR en XEPDB1
   2. Ejecutar en orden: TYPE → TYPE TABLE → PACKAGE SPEC → PACKAGE BODY
   3. Verificar STATUS = VALID en cada objeto
******************************************************************************/

/* ===========================================================================
   TABLA TEMPORAL
   =========================================================================== */
BEGIN
    EXECUTE IMMEDIATE '
        CREATE TABLE wizard_result_files (
            result_key   VARCHAR2(100)  NOT NULL,
            filename     VARCHAR2(500)  NOT NULL,
            mime_type    VARCHAR2(200)  DEFAULT ''application/octet-stream'',
            file_content BLOB           NOT NULL,
            created_at   TIMESTAMP      DEFAULT SYSTIMESTAMP,
            CONSTRAINT pk_wrf PRIMARY KEY (result_key)
        )';
EXCEPTION
    WHEN OTHERS THEN
        IF sqlcode != -955 THEN
            RAISE;
        END IF; -- ORA-955 = tabla ya existe
END;
/

/* ===========================================================================
   TYPE — ahora con bank_vars y medical_vars (c014, c015)
   =========================================================================== */
CREATE OR REPLACE TYPE t_person_data AS OBJECT (
        fullname       VARCHAR2(500),
        identification VARCHAR2(200),
        email          VARCHAR2(500),
        phone_number   VARCHAR2(200),
        position       VARCHAR2(500),
        address        VARCHAR2(1000),
        institution    VARCHAR2(500),
        bank_account   VARCHAR2(200),
        medical_cond   VARCHAR2(500),
        free_text      VARCHAR2(4000),
        name_vars      VARCHAR2(4000),   -- c011 — variantes de nombre
        id_vars        VARCHAR2(4000),   -- c012 — variantes de cédula
        phone_vars     VARCHAR2(4000),   -- c013 — variantes de teléfono
        bank_vars      VARCHAR2(4000),   -- c014 — variantes de cuenta bancaria
        medical_vars   VARCHAR2(4000)    -- c015 — variantes de condición médica
);
/

CREATE OR REPLACE TYPE t_persons_list AS
    TABLE OF t_person_data;
/

/* ===========================================================================
   PACKAGE SPEC
   =========================================================================== */
CREATE OR REPLACE PACKAGE pkg_wizard_anon AS
    c_api_base CONSTANT VARCHAR2(100) := 'http://127.0.0.1:5255';
    FUNCTION analyze_document (
        p_file_name IN VARCHAR2,
        p_jwt_token IN VARCHAR2,
        p_context   IN VARCHAR2 DEFAULT NULL
    ) RETURN CLOB;

    PROCEDURE upload_document(
        p_file_name     IN  VARCHAR2,
        p_jwt_token     IN  VARCHAR2,
        p_case_number   IN  VARCHAR2 DEFAULT NULL,
        p_office_number IN  VARCHAR2 DEFAULT NULL,
        p_persons       IN  t_persons_list,
        p_session_key   IN  VARCHAR2,          
        p_result_key    OUT VARCHAR2,
        p_result_file   OUT VARCHAR2
    );

    PROCEDURE get_result_blob (
        p_key       IN VARCHAR2,
        p_blob      OUT BLOB,
        p_filename  OUT VARCHAR2,
        p_mime_type OUT VARCHAR2
    );

    PROCEDURE clean_old_results;

END pkg_wizard_anon;
/

/* ===========================================================================
   PACKAGE BODY
   =========================================================================== */
   
CREATE OR REPLACE PACKAGE BODY pkg_wizard_anon AS

    c_crlf CONSTANT VARCHAR2(2) := CHR(13) || CHR(10);

    PROCEDURE append_text(p_blob IN OUT BLOB, p_text IN VARCHAR2) IS
    BEGIN
        IF p_text IS NOT NULL AND LENGTH(p_text) > 0 THEN
            DBMS_LOB.APPEND(p_blob, UTL_RAW.CAST_TO_RAW(p_text));
        END IF;
    END append_text;

    PROCEDURE add_field(
        p_blob     IN OUT BLOB,
        p_boundary IN     VARCHAR2,
        p_name     IN     VARCHAR2,
        p_value    IN     VARCHAR2
    ) IS
    BEGIN
        IF p_value IS NULL OR TRIM(p_value) IS NULL THEN RETURN; END IF;
        append_text(p_blob, '--' || p_boundary || c_crlf);
        append_text(p_blob, 'Content-Disposition: form-data; name="' || p_name || '"' || c_crlf);
        append_text(p_blob, c_crlf);
        append_text(p_blob, p_value || c_crlf);
    END add_field;

    PROCEDURE add_file(
        p_blob      IN OUT BLOB,
        p_boundary  IN     VARCHAR2,
        p_name      IN     VARCHAR2,
        p_filename  IN     VARCHAR2,
        p_mime      IN     VARCHAR2,
        p_content   IN     BLOB
    ) IS
    BEGIN
        append_text(p_blob, '--' || p_boundary || c_crlf);
        append_text(p_blob, 'Content-Disposition: form-data; name="' || p_name ||
                            '"; filename="' || p_filename || '"' || c_crlf);
        append_text(p_blob, 'Content-Type: ' || p_mime || c_crlf);
        append_text(p_blob, c_crlf);
        DBMS_LOB.APPEND(p_blob, p_content);
        append_text(p_blob, c_crlf);
    END add_file;

    PROCEDURE add_variations(
        p_blob     IN OUT BLOB,
        p_boundary IN     VARCHAR2,
        p_prefix   IN     VARCHAR2,
        p_values   IN     VARCHAR2
    ) IS
        l_vals  APEX_APPLICATION_GLOBAL.VC_ARR2;
        l_idx   PLS_INTEGER := 0;
    BEGIN
        IF p_values IS NULL OR TRIM(p_values) IS NULL THEN RETURN; END IF;
        l_vals := APEX_UTIL.STRING_TO_TABLE(p_values, '|');
        FOR i IN 1..l_vals.COUNT LOOP
            IF TRIM(l_vals(i)) IS NOT NULL THEN
                add_field(p_blob, p_boundary,
                          p_prefix || '[' || l_idx || ']',
                          TRIM(l_vals(i)));
                l_idx := l_idx + 1;
            END IF;
        END LOOP;
    END add_variations;

    -- Versión para respuesta TEXT (analyze)
    FUNCTION send_request_text(
        p_url       IN  VARCHAR2,
        p_token     IN  VARCHAR2,
        p_boundary  IN  VARCHAR2,
        p_body      IN  BLOB,
        p_response  OUT CLOB
    ) RETURN NUMBER IS
        l_req        UTL_HTTP.REQ;
        l_resp       UTL_HTTP.RESP;
        l_status     NUMBER;
        l_offset     INTEGER := 1;
        l_chunk_size INTEGER := 16384;
        l_raw_chunk  RAW(16384);
        l_body_len   INTEGER;
        l_text_buf   VARCHAR2(32767);
    BEGIN
        l_body_len := DBMS_LOB.GETLENGTH(p_body);
        l_req := UTL_HTTP.BEGIN_REQUEST(p_url, 'POST', 'HTTP/1.1');
        UTL_HTTP.SET_HEADER(l_req, 'Authorization', 'Bearer ' || p_token);
        UTL_HTTP.SET_HEADER(l_req, 'Content-Type',
                            'multipart/form-data; boundary=' || p_boundary);
        UTL_HTTP.SET_HEADER(l_req, 'Content-Length', l_body_len);
        WHILE l_offset <= l_body_len LOOP
            l_chunk_size := LEAST(16384, l_body_len - l_offset + 1);
            l_raw_chunk  := DBMS_LOB.SUBSTR(p_body, l_chunk_size, l_offset);
            UTL_HTTP.WRITE_RAW(l_req, l_raw_chunk);
            l_offset := l_offset + l_chunk_size;
        END LOOP;
        l_resp   := UTL_HTTP.GET_RESPONSE(l_req);
        l_status := l_resp.status_code;
        p_response := EMPTY_CLOB();
        DBMS_LOB.CREATETEMPORARY(p_response, TRUE);
        LOOP
            BEGIN
                UTL_HTTP.READ_TEXT(l_resp, l_text_buf, 32767);
                DBMS_LOB.APPEND(p_response, l_text_buf);
            EXCEPTION WHEN UTL_HTTP.END_OF_BODY THEN EXIT; END;
        END LOOP;
        UTL_HTTP.END_RESPONSE(l_resp);
        RETURN l_status;
    EXCEPTION
        WHEN OTHERS THEN
            BEGIN UTL_HTTP.END_RESPONSE(l_resp); EXCEPTION WHEN OTHERS THEN NULL; END;
            RAISE;
    END send_request_text;

    -- Versión para respuesta BINARY (upload)
    FUNCTION send_request_binary(
        p_url       IN  VARCHAR2,
        p_token     IN  VARCHAR2,
        p_boundary  IN  VARCHAR2,
        p_body      IN  BLOB,
        p_resp_blob OUT BLOB
    ) RETURN NUMBER IS
        l_req        UTL_HTTP.REQ;
        l_resp       UTL_HTTP.RESP;
        l_status     NUMBER;
        l_offset     INTEGER := 1;
        l_chunk_size INTEGER := 16384;
        l_raw_chunk  RAW(16384);
        l_body_len   INTEGER;
    BEGIN
        l_body_len := DBMS_LOB.GETLENGTH(p_body);
        l_req := UTL_HTTP.BEGIN_REQUEST(p_url, 'POST', 'HTTP/1.1');
        UTL_HTTP.SET_HEADER(l_req, 'Authorization', 'Bearer ' || p_token);
        UTL_HTTP.SET_HEADER(l_req, 'Content-Type',
                            'multipart/form-data; boundary=' || p_boundary);
        UTL_HTTP.SET_HEADER(l_req, 'Content-Length', l_body_len);
        WHILE l_offset <= l_body_len LOOP
            l_chunk_size := LEAST(16384, l_body_len - l_offset + 1);
            l_raw_chunk  := DBMS_LOB.SUBSTR(p_body, l_chunk_size, l_offset);
            UTL_HTTP.WRITE_RAW(l_req, l_raw_chunk);
            l_offset := l_offset + l_chunk_size;
        END LOOP;
        l_resp   := UTL_HTTP.GET_RESPONSE(l_req);
        l_status := l_resp.status_code;
        -- Leer respuesta binaria
        DBMS_LOB.CREATETEMPORARY(p_resp_blob, TRUE);
        LOOP
            BEGIN
                UTL_HTTP.READ_RAW(l_resp, l_raw_chunk, 16384);
                DBMS_LOB.APPEND(p_resp_blob, l_raw_chunk);
            EXCEPTION WHEN UTL_HTTP.END_OF_BODY THEN EXIT; END;
        END LOOP;
        UTL_HTTP.END_RESPONSE(l_resp);
        RETURN l_status;
    EXCEPTION
        WHEN OTHERS THEN
            BEGIN UTL_HTTP.END_RESPONSE(l_resp); EXCEPTION WHEN OTHERS THEN NULL; END;
            RAISE;
    END send_request_binary;

    FUNCTION analyze_document(
        p_file_name  IN VARCHAR2,
        p_jwt_token  IN VARCHAR2,
        p_context    IN VARCHAR2 DEFAULT NULL
    ) RETURN CLOB IS
        l_file_blob  BLOB;
        l_filename   VARCHAR2(500);
        l_mime_type  VARCHAR2(200);
        l_boundary   VARCHAR2(60);
        l_body       BLOB;
        l_response   CLOB;
        l_status     NUMBER;
    BEGIN
        SELECT blob_content, filename,
               NVL(mime_type, 'application/octet-stream')
        INTO   l_file_blob, l_filename, l_mime_type
        FROM   apex_application_temp_files
        WHERE  name = p_file_name;

        l_boundary := 'APEXBoundary' || REPLACE(SYS_GUID(), '-', '');
        DBMS_LOB.CREATETEMPORARY(l_body, TRUE);
        add_file(l_body, l_boundary, 'File', l_filename, l_mime_type, l_file_blob);
        add_field(l_body, l_boundary, 'AdditionalContext', p_context);
        append_text(l_body, '--' || l_boundary || '--' || c_crlf);

        l_status := send_request_text(
            p_url      => c_api_base || '/api/documents/analyze',
            p_token    => p_jwt_token,
            p_boundary => l_boundary,
            p_body     => l_body,
            p_response => l_response
        );
        DBMS_LOB.FREETEMPORARY(l_body);

        IF l_status NOT IN (200, 201) THEN
            RAISE_APPLICATION_ERROR(-20001,
                'Error en análisis IA (HTTP ' || l_status || '): ' ||
                SUBSTR(l_response, 1, 500));
        END IF;
        RETURN l_response;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            RAISE_APPLICATION_ERROR(-20002,
                'Archivo no encontrado en APEX temp files: ' || p_file_name);
    END analyze_document;

    PROCEDURE upload_document(
        p_file_name     IN  VARCHAR2,
        p_jwt_token     IN  VARCHAR2,
        p_case_number   IN  VARCHAR2 DEFAULT NULL,
        p_office_number IN  VARCHAR2 DEFAULT NULL,
        p_persons       IN  t_persons_list,
        p_session_key   IN  VARCHAR2,
        p_result_key    OUT VARCHAR2,
        p_result_file   OUT VARCHAR2
    ) IS
        l_file_blob   BLOB;
        l_filename    VARCHAR2(500);
        l_mime_type   VARCHAR2(200);
        l_boundary    VARCHAR2(60);
        l_body        BLOB;
        l_result_blob BLOB;
        l_status      NUMBER;
        l_prefix      VARCHAR2(50);
        l_person      t_person_data;
        l_ext         VARCHAR2(10);
        l_result_mime VARCHAR2(200);
    BEGIN
        clean_old_results;

        SELECT file_content, filename,
               NVL(mime_type, 'application/octet-stream')
        INTO   l_file_blob, l_filename, l_mime_type
        FROM   wizard_session_files
        WHERE  session_id = p_session_key;

        l_boundary := 'APEXBoundary' || REPLACE(SYS_GUID(), '-', '');
        DBMS_LOB.CREATETEMPORARY(l_body, TRUE);

        add_file(l_body, l_boundary, 'File', l_filename, l_mime_type, l_file_blob);
        add_field(l_body, l_boundary, 'GeneralData.CaseNumber',   p_case_number);
        add_field(l_body, l_boundary, 'GeneralData.OfficeNumber', p_office_number);

        IF p_persons IS NOT NULL AND p_persons.COUNT > 0 THEN
            FOR i IN 0..p_persons.COUNT - 1 LOOP
                l_person := p_persons(i + 1);
                l_prefix := 'Persons[' || i || ']';
                add_field(l_body, l_boundary, l_prefix || '.FullName',         l_person.fullname);
                add_field(l_body, l_boundary, l_prefix || '.Identification',   l_person.identification);
                add_field(l_body, l_boundary, l_prefix || '.Email',            l_person.email);
                add_field(l_body, l_boundary, l_prefix || '.PhoneNumber',      l_person.phone_number);
                add_field(l_body, l_boundary, l_prefix || '.Position',         l_person.position);
                add_field(l_body, l_boundary, l_prefix || '.Address',          l_person.address);
                add_field(l_body, l_boundary, l_prefix || '.Institution',      l_person.institution);
                add_field(l_body, l_boundary, l_prefix || '.BankAccount',      l_person.bank_account);
                add_field(l_body, l_boundary, l_prefix || '.MedicalCondition', l_person.medical_cond);
                add_field(l_body, l_boundary, l_prefix || '.FreeText',         l_person.free_text);
                add_variations(l_body, l_boundary,
                               l_prefix || '.NameVariations',        l_person.name_vars);
                add_variations(l_body, l_boundary,
                               l_prefix || '.IdVariations',          l_person.id_vars);
                add_variations(l_body, l_boundary,
                               l_prefix || '.PhoneVariations',       l_person.phone_vars);
                add_variations(l_body, l_boundary,
                               l_prefix || '.BankAccountVariations', l_person.bank_vars);
                add_variations(l_body, l_boundary,
                               l_prefix || '.MedicalVariations',     l_person.medical_vars);
            END LOOP;
        END IF;

        append_text(l_body, '--' || l_boundary || '--' || c_crlf);

        -- Usar send_request_binary — crea el LOB internamente y lo retorna lleno
        l_status := send_request_binary(
            p_url       => c_api_base || '/api/documents/upload',
            p_token     => p_jwt_token,
            p_boundary  => l_boundary,
            p_body      => l_body,
            p_resp_blob => l_result_blob
        );
        DBMS_LOB.FREETEMPORARY(l_body);

        IF l_status NOT IN (200, 201) THEN
            RAISE_APPLICATION_ERROR(-20003,
                'Error al anonimizar (HTTP ' || l_status ||
                '). Verificá los datos e intentá de nuevo.');
        END IF;

        l_ext := LOWER(SUBSTR(l_filename, INSTR(l_filename, '.', -1) + 1));
        l_result_mime := CASE
            WHEN l_ext = 'pdf'  THEN 'application/pdf'
            WHEN l_ext = 'docx' THEN
                'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
            ELSE 'application/octet-stream'
        END;

        p_result_file := 'ANON_' || l_filename;
        p_result_key  := 'WIZ_' || v('APP_SESSION') || '_' ||
                         TO_CHAR(SYSTIMESTAMP, 'YYYYMMDDHH24MISSFF3');

        INSERT INTO wizard_result_files
            (result_key, filename, mime_type, file_content, created_at)
        VALUES
            (p_result_key, p_result_file, l_result_mime, l_result_blob, SYSTIMESTAMP);
        COMMIT;

        DBMS_LOB.FREETEMPORARY(l_result_blob);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            RAISE_APPLICATION_ERROR(-20002,
                'Archivo de sesión no encontrado. Volvé al Paso 1 y cargá el archivo nuevamente.');
    END upload_document;

    PROCEDURE get_result_blob(
        p_key       IN  VARCHAR2,
        p_blob      OUT BLOB,
        p_filename  OUT VARCHAR2,
        p_mime_type OUT VARCHAR2
    ) IS
    BEGIN
        SELECT file_content, filename, mime_type
        INTO   p_blob, p_filename, p_mime_type
        FROM   wizard_result_files
        WHERE  result_key = p_key;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            RAISE_APPLICATION_ERROR(-20004,
                'Resultado no encontrado o expirado. Volvé a anonimizar el documento.');
    END get_result_blob;

    PROCEDURE clean_old_results IS
    BEGIN
        DELETE FROM wizard_result_files
        WHERE created_at < SYSTIMESTAMP - INTERVAL '2' HOUR;
        DELETE FROM wizard_session_files
        WHERE created_at < SYSTIMESTAMP - INTERVAL '2' HOUR;
        COMMIT;
    EXCEPTION WHEN OTHERS THEN
        ROLLBACK;
    END clean_old_results;

END pkg_wizard_anon;
/

/* ===========================================================================
   VERIFICAR
   =========================================================================== */
SELECT
    object_name,
    object_type,
    status
FROM
    user_objects
WHERE
    object_name IN ( 'PKG_WIZARD_ANON', 'T_PERSON_DATA', 'T_PERSONS_LIST', 'WIZARD_RESULT_FILES' )
ORDER BY
    object_type,
    object_name;