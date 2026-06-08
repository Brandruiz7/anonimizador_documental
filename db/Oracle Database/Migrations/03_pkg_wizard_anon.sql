/******************************************************************************
 Archivo    : 03_pkg_wizard_anon.sql
 Descripción: Package del Wizard de Anonimización — URL de la API y retención
              de archivos leídas desde anonimizador_config.
 Autor      : Ruiz
 Esquema    : ANONIMIZADOR @ XEPDB1

 CAMBIOS respecto a la versión anterior:
   - Eliminada la constante c_api_base hardcodeada del SPEC
   - Eliminado el INTERVAL '2' HOUR hardcodeado en clean_old_results
   - Agregada función privada get_config() que lee anonimizador_config
   - La URL se resuelve con get_config('API_BASE_URL')
   - La retención se resuelve con get_config('SESSION_RETENTION_H')

 INSTRUCCIONES:
   1. Ejecutar primero 01_anonimizador_config.sql
   2. Ejecutar TYPE → TYPE TABLE si no existen todavía
   3. Ejecutar el bloque SPEC
   4. Verificar STATUS = VALID
   5. Ejecutar el bloque BODY
   6. Verificar STATUS = VALID en ambos objetos
******************************************************************************/

/* ===========================================================================
   TYPE — solo si no existen (idempotente)
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

CREATE OR REPLACE TYPE t_persons_list AS TABLE OF t_person_data;
/

/* ===========================================================================
   PACKAGE SPEC
   Nota: c_api_base se eliminó del spec — la URL ahora la gestiona
   get_config() en el body, igual que en pkg_auth_anonimizador.
   =========================================================================== */
CREATE OR REPLACE PACKAGE pkg_wizard_anon AS

    -- Llama a POST /api/documents/analyze y devuelve el JSON crudo
    FUNCTION analyze_document (
        p_file_name IN VARCHAR2,
        p_jwt_token IN VARCHAR2,
        p_context   IN VARCHAR2 DEFAULT NULL
    ) RETURN CLOB;

    -- Llama a analyze_document, parsea el JSON y puebla WIZARD_PERSONS
    -- p_file_name = valor de :P20_FILE (nombre en apex_application_temp_files)
    -- Llamar en la transición Paso 1 → Paso 2 cuando P20_MODE = 'IA'
    PROCEDURE analyze_document_ia (
        p_file_name IN VARCHAR2,
        p_jwt_token IN VARCHAR2,
        p_context   IN VARCHAR2 DEFAULT NULL
    );

    -- Llama a POST /api/documents/upload y guarda el resultado anonimizado
    PROCEDURE upload_document (
        p_file_name     IN  VARCHAR2,
        p_jwt_token     IN  VARCHAR2,
        p_case_number   IN  VARCHAR2 DEFAULT NULL,
        p_office_number IN  VARCHAR2 DEFAULT NULL,
        p_persons       IN  t_persons_list,
        p_session_key   IN  VARCHAR2,
        p_result_key    OUT VARCHAR2,
        p_result_file   OUT VARCHAR2
    );

    -- Recupera el archivo anonimizado para descarga o preview
    PROCEDURE get_result_blob (
        p_key       IN  VARCHAR2,
        p_blob      OUT BLOB,
        p_filename  OUT VARCHAR2,
        p_mime_type OUT VARCHAR2
    );

    -- Limpia wizard_session_files y wizard_result_files según SESSION_RETENTION_H
    PROCEDURE clean_old_results;

END pkg_wizard_anon;
/

-- =============================================
-- Verificar SPEC
-- =============================================
SELECT object_name, object_type, status
FROM   user_objects
WHERE  object_name = 'PKG_WIZARD_ANON'
ORDER BY object_type;

/* ===========================================================================
   PACKAGE BODY
   =========================================================================== */
CREATE OR REPLACE PACKAGE BODY pkg_wizard_anon AS

    c_crlf CONSTANT VARCHAR2(2) := chr(13) || chr(10);

    -- -------------------------------------------------------------------------
    -- get_config — lee un valor de anonimizador_config por clave.
    -- Mismo patrón que pkg_auth_anonimizador para consistencia.
    -- -------------------------------------------------------------------------
    FUNCTION get_config(p_key IN VARCHAR2) RETURN VARCHAR2 IS
        l_value anonimizador_config.config_value%TYPE;
    BEGIN
        SELECT config_value
        INTO   l_value
        FROM   anonimizador_config
        WHERE  config_key = p_key;

        RETURN l_value;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            raise_application_error(-20099,
                'Configuración no encontrada: ' || p_key ||
                '. Verificar tabla anonimizador_config.');
    END get_config;

    -- -------------------------------------------------------------------------
    -- Helpers de construcción multipart
    -- -------------------------------------------------------------------------
    PROCEDURE append_text (
        p_blob IN OUT BLOB,
        p_text IN VARCHAR2
    ) IS
    BEGIN
        IF p_text IS NOT NULL AND length(p_text) > 0 THEN
            dbms_lob.append(p_blob, utl_raw.cast_to_raw(p_text));
        END IF;
    END append_text;

    PROCEDURE add_field (
        p_blob     IN OUT BLOB,
        p_boundary IN VARCHAR2,
        p_name     IN VARCHAR2,
        p_value    IN VARCHAR2
    ) IS
    BEGIN
        IF p_value IS NULL OR TRIM(p_value) IS NULL THEN RETURN; END IF;
        append_text(p_blob, '--' || p_boundary || c_crlf);
        append_text(p_blob, 'Content-Disposition: form-data; name="' || p_name || '"' || c_crlf);
        append_text(p_blob, c_crlf);
        append_text(p_blob, p_value || c_crlf);
    END add_field;

    PROCEDURE add_file (
        p_blob     IN OUT BLOB,
        p_boundary IN VARCHAR2,
        p_name     IN VARCHAR2,
        p_filename IN VARCHAR2,
        p_mime     IN VARCHAR2,
        p_content  IN BLOB
    ) IS
    BEGIN
        append_text(p_blob, '--' || p_boundary || c_crlf);
        append_text(p_blob, 'Content-Disposition: form-data; name="' || p_name
                            || '"; filename="' || p_filename || '"' || c_crlf);
        append_text(p_blob, 'Content-Type: ' || p_mime || c_crlf);
        append_text(p_blob, c_crlf);
        dbms_lob.append(p_blob, p_content);
        append_text(p_blob, c_crlf);
    END add_file;

    PROCEDURE add_variations (
        p_blob     IN OUT BLOB,
        p_boundary IN VARCHAR2,
        p_prefix   IN VARCHAR2,
        p_values   IN VARCHAR2
    ) IS
        l_vals apex_application_global.vc_arr2;
        l_idx  PLS_INTEGER := 0;
    BEGIN
        IF p_values IS NULL OR TRIM(p_values) IS NULL THEN RETURN; END IF;
        l_vals := apex_util.string_to_table(p_values, '|');
        FOR i IN 1..l_vals.count LOOP
            IF TRIM(l_vals(i)) IS NOT NULL THEN
                add_field(p_blob, p_boundary,
                          p_prefix || '[' || l_idx || ']',
                          trim(l_vals(i)));
                l_idx := l_idx + 1;
            END IF;
        END LOOP;
    END add_variations;

    -- -------------------------------------------------------------------------
    -- HTTP helpers
    -- -------------------------------------------------------------------------
    FUNCTION send_request_text (
        p_url      IN  VARCHAR2,
        p_token    IN  VARCHAR2,
        p_boundary IN  VARCHAR2,
        p_body     IN  BLOB,
        p_response OUT CLOB
    ) RETURN NUMBER IS
        l_req        utl_http.req;
        l_resp       utl_http.resp;
        l_status     NUMBER;
        l_offset     INTEGER := 1;
        l_chunk_size INTEGER := 16384;
        l_raw_chunk  RAW(16384);
        l_body_len   INTEGER;
        l_text_buf   VARCHAR2(32767);
    BEGIN
        l_body_len := dbms_lob.getlength(p_body);
        l_req := utl_http.begin_request(p_url, 'POST', 'HTTP/1.1');
        utl_http.set_header(l_req, 'Authorization',  'Bearer ' || p_token);
        utl_http.set_header(l_req, 'Content-Type',   'multipart/form-data; boundary=' || p_boundary);
        utl_http.set_header(l_req, 'Content-Length',  l_body_len);
        WHILE l_offset <= l_body_len LOOP
            l_chunk_size := least(16384, l_body_len - l_offset + 1);
            l_raw_chunk  := dbms_lob.substr(p_body, l_chunk_size, l_offset);
            utl_http.write_raw(l_req, l_raw_chunk);
            l_offset := l_offset + l_chunk_size;
        END LOOP;
        l_resp   := utl_http.get_response(l_req);
        l_status := l_resp.status_code;
        p_response := empty_clob();
        dbms_lob.createtemporary(p_response, TRUE);
        LOOP
            BEGIN
                utl_http.read_text(l_resp, l_text_buf, 32767);
                dbms_lob.append(p_response, l_text_buf);
            EXCEPTION
                WHEN utl_http.end_of_body THEN EXIT;
            END;
        END LOOP;
        utl_http.end_response(l_resp);
        RETURN l_status;
    EXCEPTION
        WHEN OTHERS THEN
            BEGIN utl_http.end_response(l_resp); EXCEPTION WHEN OTHERS THEN NULL; END;
            RAISE;
    END send_request_text;

    FUNCTION send_request_binary (
        p_url       IN  VARCHAR2,
        p_token     IN  VARCHAR2,
        p_boundary  IN  VARCHAR2,
        p_body      IN  BLOB,
        p_resp_blob OUT BLOB
    ) RETURN NUMBER IS
        l_req        utl_http.req;
        l_resp       utl_http.resp;
        l_status     NUMBER;
        l_offset     INTEGER := 1;
        l_chunk_size INTEGER := 16384;
        l_raw_chunk  RAW(16384);
        l_body_len   INTEGER;
    BEGIN
        l_body_len := dbms_lob.getlength(p_body);
        l_req := utl_http.begin_request(p_url, 'POST', 'HTTP/1.1');
        utl_http.set_header(l_req, 'Authorization',  'Bearer ' || p_token);
        utl_http.set_header(l_req, 'Content-Type',   'multipart/form-data; boundary=' || p_boundary);
        utl_http.set_header(l_req, 'Content-Length',  l_body_len);
        WHILE l_offset <= l_body_len LOOP
            l_chunk_size := least(16384, l_body_len - l_offset + 1);
            l_raw_chunk  := dbms_lob.substr(p_body, l_chunk_size, l_offset);
            utl_http.write_raw(l_req, l_raw_chunk);
            l_offset := l_offset + l_chunk_size;
        END LOOP;
        l_resp   := utl_http.get_response(l_req);
        l_status := l_resp.status_code;
        dbms_lob.createtemporary(p_resp_blob, TRUE);
        LOOP
            BEGIN
                utl_http.read_raw(l_resp, l_raw_chunk, 16384);
                dbms_lob.append(p_resp_blob, l_raw_chunk);
            EXCEPTION
                WHEN utl_http.end_of_body THEN EXIT;
            END;
        END LOOP;
        utl_http.end_response(l_resp);
        RETURN l_status;
    EXCEPTION
        WHEN OTHERS THEN
            BEGIN utl_http.end_response(l_resp); EXCEPTION WHEN OTHERS THEN NULL; END;
            RAISE;
    END send_request_binary;

    -- -------------------------------------------------------------------------
    -- analyze_document — llama a la API y devuelve el JSON crudo
    -- -------------------------------------------------------------------------
    FUNCTION analyze_document (
        p_file_name IN VARCHAR2,
        p_jwt_token IN VARCHAR2,
        p_context   IN VARCHAR2 DEFAULT NULL
    ) RETURN CLOB IS
        l_file_blob BLOB;
        l_filename  VARCHAR2(500);
        l_mime_type VARCHAR2(200);
        l_boundary  VARCHAR2(60);
        l_body      BLOB;
        l_response  CLOB;
        l_status    NUMBER;
    BEGIN
        SELECT blob_content,
               filename,
               nvl(mime_type, 'application/octet-stream')
        INTO   l_file_blob, l_filename, l_mime_type
        FROM   apex_application_temp_files
        WHERE  name = p_file_name;

        l_boundary := 'APEXBoundary' || replace(sys_guid(), '-', '');
        dbms_lob.createtemporary(l_body, TRUE);
        add_file (l_body, l_boundary, 'File', l_filename, l_mime_type, l_file_blob);
        add_field(l_body, l_boundary, 'AdditionalContext', p_context);
        append_text(l_body, '--' || l_boundary || '--' || c_crlf);

        l_status := send_request_text(
            p_url      => get_config('API_BASE_URL') || '/api/documents/analyze',
            p_token    => p_jwt_token,
            p_boundary => l_boundary,
            p_body     => l_body,
            p_response => l_response
        );

        dbms_lob.freetemporary(l_body);

        IF l_status NOT IN (200, 201) THEN
            raise_application_error(-20001,
                'Error en análisis IA (HTTP ' || l_status || '): '
                || substr(l_response, 1, 500));
        END IF;

        RETURN l_response;
    EXCEPTION
        WHEN no_data_found THEN
            raise_application_error(-20002,
                'Archivo no encontrado en APEX temp files: ' || p_file_name);
    END analyze_document;

    -- -------------------------------------------------------------------------
    -- analyze_document_ia — llama a analyze_document, parsea el JSON
    --                        y puebla la colección WIZARD_PERSONS
    --
    -- p_file_name = :P20_FILE  (nombre interno en apex_application_temp_files)
    -- Llamar en la transición Paso 1 → Paso 2 solo cuando :P20_MODE = 'IA'
    --
    -- Mapeo JSON → colección WIZARD_PERSONS (c001-c015):
    --   fullName         → c001    institution      → c007
    --   identification   → c002    bankAccount      → c008
    --   email            → c003    medicalCondition → c009
    --   phoneNumber      → c004    (free_text)      → c010  siempre NULL
    --   position         → c005    nameVariations[] → c011  aplanado con pipes
    --   address          → c006    c012-c015        → NULL  usuario agrega si quiere
    -- -------------------------------------------------------------------------
    PROCEDURE analyze_document_ia (
        p_file_name IN VARCHAR2,
        p_jwt_token IN VARCHAR2,
        p_context   IN VARCHAR2 DEFAULT NULL
    ) IS
        l_json       CLOB;
        l_count      INTEGER;
        l_base       VARCHAR2(200);
        l_vars_count INTEGER;
        l_vars_str   VARCHAR2(4000);
        l_sep        VARCHAR2(2);
    BEGIN
        -- 1. Llamar a la API y obtener el JSON
        l_json := analyze_document(
            p_file_name => p_file_name,
            p_jwt_token => p_jwt_token,
            p_context   => p_context
        );

        -- 2. Parsear
        apex_json.parse(l_json);
        l_count := apex_json.get_count(p_path => 'detectedPersons');

        -- 3. Limpiar y recrear la colección
        IF apex_collection.collection_exists('WIZARD_PERSONS') THEN
            apex_collection.delete_collection('WIZARD_PERSONS');
        END IF;
        apex_collection.create_collection('WIZARD_PERSONS');

        -- 4. Una fila por persona detectada
        FOR i IN 1..l_count LOOP
            l_base     := 'detectedPersons[' || i || ']';
            l_vars_str := '';
            l_sep      := '';

            -- Aplanar nameVariations[] → "var1|var2|var3" para c011
            -- El separador | es el que usa add_variations al enviar a la API
            BEGIN
                l_vars_count := apex_json.get_count(p_path => l_base || '.nameVariations');
                FOR j IN 1..l_vars_count LOOP
                    DECLARE
                        l_v VARCHAR2(500);
                    BEGIN
                        l_v := TRIM(apex_json.get_varchar2(
                                   p_path => l_base || '.nameVariations[' || j || ']'));
                        IF l_v IS NOT NULL AND l_v != 'NONE' THEN
                            l_vars_str := l_vars_str || l_sep || l_v;
                            l_sep      := '|';
                        END IF;
                    END;
                END LOOP;
            EXCEPTION
                WHEN OTHERS THEN l_vars_str := '';
            END;

            apex_collection.add_member(
                p_collection_name => 'WIZARD_PERSONS',
                p_c001 => apex_json.get_varchar2(p_path => l_base || '.fullName'),
                p_c002 => apex_json.get_varchar2(p_path => l_base || '.identification'),
                p_c003 => apex_json.get_varchar2(p_path => l_base || '.email'),
                p_c004 => apex_json.get_varchar2(p_path => l_base || '.phoneNumber'),
                p_c005 => apex_json.get_varchar2(p_path => l_base || '.position'),
                p_c006 => apex_json.get_varchar2(p_path => l_base || '.address'),
                p_c007 => apex_json.get_varchar2(p_path => l_base || '.institution'),
                p_c008 => apex_json.get_varchar2(p_path => l_base || '.bankAccount'),
                p_c009 => apex_json.get_varchar2(p_path => l_base || '.medicalCondition'),
                p_c010 => NULL,                   -- free_text: solo modo manual
                p_c011 => NULLIF(l_vars_str, ''), -- nameVariations separadas por |
                p_c012 => NULL,                   -- id_vars
                p_c013 => NULL,                   -- phone_vars
                p_c014 => NULL,                   -- bank_vars
                p_c015 => NULL                    -- medical_vars
            );
        END LOOP;

        -- 5. Liberar CLOB
        IF dbms_lob.istemporary(l_json) = 1 THEN
            dbms_lob.freetemporary(l_json);
        END IF;

    EXCEPTION
        WHEN OTHERS THEN
            IF dbms_lob.istemporary(l_json) = 1 THEN
                dbms_lob.freetemporary(l_json);
            END IF;
            apex_debug.error('analyze_document_ia — file=%s error=%s',
                             p_file_name, SQLERRM);
            RAISE;
    END analyze_document_ia;

    -- -------------------------------------------------------------------------
    -- upload_document — anonimiza y guarda el resultado
    -- -------------------------------------------------------------------------
    PROCEDURE upload_document (
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

        SELECT file_content,
               filename,
               nvl(mime_type, 'application/octet-stream')
        INTO   l_file_blob, l_filename, l_mime_type
        FROM   wizard_session_files
        WHERE  session_id = p_session_key;

        l_boundary := 'APEXBoundary' || replace(sys_guid(), '-', '');
        dbms_lob.createtemporary(l_body, TRUE);
        add_file (l_body, l_boundary, 'File', l_filename, l_mime_type, l_file_blob);
        add_field(l_body, l_boundary, 'GeneralData.CaseNumber',   p_case_number);
        add_field(l_body, l_boundary, 'GeneralData.OfficeNumber', p_office_number);

        IF p_persons IS NOT NULL AND p_persons.count > 0 THEN
            FOR i IN 0..p_persons.count - 1 LOOP
                l_person := p_persons(i + 1);
                l_prefix := 'Persons[' || i || ']';
                add_field(l_body, l_boundary, l_prefix || '.FullName',          l_person.fullname);
                add_field(l_body, l_boundary, l_prefix || '.Identification',    l_person.identification);
                add_field(l_body, l_boundary, l_prefix || '.Email',             l_person.email);
                add_field(l_body, l_boundary, l_prefix || '.PhoneNumber',       l_person.phone_number);
                add_field(l_body, l_boundary, l_prefix || '.Position',          l_person.position);
                add_field(l_body, l_boundary, l_prefix || '.Address',           l_person.address);
                add_field(l_body, l_boundary, l_prefix || '.Institution',       l_person.institution);
                add_field(l_body, l_boundary, l_prefix || '.BankAccount',       l_person.bank_account);
                add_field(l_body, l_boundary, l_prefix || '.MedicalCondition',  l_person.medical_cond);
                add_field(l_body, l_boundary, l_prefix || '.FreeText',          l_person.free_text);
                add_variations(l_body, l_boundary, l_prefix || '.NameVariations',        l_person.name_vars);
                add_variations(l_body, l_boundary, l_prefix || '.IdVariations',          l_person.id_vars);
                add_variations(l_body, l_boundary, l_prefix || '.PhoneVariations',       l_person.phone_vars);
                add_variations(l_body, l_boundary, l_prefix || '.BankAccountVariations', l_person.bank_vars);
                add_variations(l_body, l_boundary, l_prefix || '.MedicalVariations',     l_person.medical_vars);
            END LOOP;
        END IF;

        append_text(l_body, '--' || l_boundary || '--' || c_crlf);

        l_status := send_request_binary(
            p_url       => get_config('API_BASE_URL') || '/api/documents/upload',
            p_token     => p_jwt_token,
            p_boundary  => l_boundary,
            p_body      => l_body,
            p_resp_blob => l_result_blob
        );

        dbms_lob.freetemporary(l_body);

        IF l_status NOT IN (200, 201) THEN
            raise_application_error(-20003,
                'Error al anonimizar (HTTP ' || l_status
                || '). Verificá los datos e intentá de nuevo.');
        END IF;

        l_ext := lower(substr(l_filename, instr(l_filename, '.', -1) + 1));
        l_result_mime :=
            CASE
                WHEN l_ext = 'pdf'  THEN 'application/pdf'
                WHEN l_ext = 'docx' THEN
                    'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
                ELSE 'application/octet-stream'
            END;

        p_result_file := 'ANON_' || l_filename;
        p_result_key  := 'WIZ_' || v('APP_SESSION') || '_'
                         || to_char(systimestamp, 'YYYYMMDDHH24MISSFF3');

        INSERT INTO wizard_result_files (result_key, filename, mime_type, file_content, created_at)
        VALUES (p_result_key, p_result_file, l_result_mime, l_result_blob, systimestamp);

        COMMIT;
        dbms_lob.freetemporary(l_result_blob);
    EXCEPTION
        WHEN no_data_found THEN
            raise_application_error(-20002,
                'Archivo de sesión no encontrado. Volvé al Paso 1 y cargá el archivo nuevamente.');
    END upload_document;

    -- -------------------------------------------------------------------------
    -- get_result_blob — recupera el archivo anonimizado para descarga/preview
    -- -------------------------------------------------------------------------
    PROCEDURE get_result_blob (
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
        WHEN no_data_found THEN
            raise_application_error(-20004,
                'Resultado no encontrado o expirado. Volvé a anonimizar el documento.');
    END get_result_blob;

    -- -------------------------------------------------------------------------
    -- clean_old_results — limpia registros según SESSION_RETENTION_H
    -- -------------------------------------------------------------------------
    PROCEDURE clean_old_results IS
        l_hours NUMBER;
    BEGIN
        -- Leer retención desde configuración (default 2 si no existe)
        BEGIN
            l_hours := TO_NUMBER(get_config('SESSION_RETENTION_H'));
        EXCEPTION
            WHEN OTHERS THEN l_hours := 2;
        END;

        DELETE FROM wizard_result_files
        WHERE  created_at < systimestamp - (l_hours / 24);

        DELETE FROM wizard_session_files
        WHERE  created_at < systimestamp - (l_hours / 24);

        COMMIT;
    EXCEPTION
        WHEN OTHERS THEN ROLLBACK;
    END clean_old_results;

END pkg_wizard_anon;
/

/* ===========================================================================
   VERIFICAR FINAL
   =========================================================================== */
SELECT object_name, object_type, status
FROM   user_objects
WHERE  object_name IN ('PKG_WIZARD_ANON', 'PKG_AUTH_ANONIMIZADOR',
                       'T_PERSON_DATA', 'T_PERSONS_LIST',
                       'WIZARD_RESULT_FILES', 'ANONIMIZADOR_CONFIG')
ORDER BY object_type, object_name;