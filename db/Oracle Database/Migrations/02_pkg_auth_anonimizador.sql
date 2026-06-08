/******************************************************************************
 Archivo    : 02_pkg_auth_anonimizador.sql
 Descripción: Package de autenticación — URL de la API leída desde
              anonimizador_config en lugar de estar hardcodeada.
 Autor      : Ruiz
 Esquema    : ANONIMIZADOR @ XEPDB1

 CAMBIOS respecto a la versión anterior:
   - Eliminada la constante c_api_base_url hardcodeada
   - Agregada función privada get_config() que lee anonimizador_config
   - La URL se resuelve en tiempo de ejecución con get_config('API_BASE_URL')

 INSTRUCCIONES:
   1. Ejecutar primero 01_anonimizador_config.sql
   2. Ejecutar el bloque SPEC
   3. Verificar STATUS = VALID
   4. Ejecutar el bloque BODY
   5. Verificar STATUS = VALID en ambos objetos
******************************************************************************/

-- =============================================
-- PACKAGE SPEC  (sin cambios de firma)
-- =============================================
CREATE OR REPLACE PACKAGE pkg_auth_anonimizador AS

    FUNCTION authenticate_user(
        p_username IN VARCHAR2,
        p_password IN VARCHAR2
    ) RETURN BOOLEAN;

END pkg_auth_anonimizador;
/

-- =============================================
-- Verificar SPEC
-- =============================================
SELECT object_name, object_type, status
FROM   user_objects
WHERE  object_name = 'PKG_AUTH_ANONIMIZADOR'
ORDER BY object_type;

-- =============================================
-- PACKAGE BODY
-- =============================================
CREATE OR REPLACE PACKAGE BODY pkg_auth_anonimizador AS

    -- -------------------------------------------------------------------------
    -- get_config — lee un valor de anonimizador_config por clave.
    -- Lanza excepción con mensaje claro si la clave no existe,
    -- para evitar errores silenciosos por configuración incompleta.
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
    -- authenticate_user — valida credenciales contra la API .NET
    -- -------------------------------------------------------------------------
    FUNCTION authenticate_user(
        p_username IN VARCHAR2,
        p_password IN VARCHAR2
    ) RETURN BOOLEAN IS
        l_api_base  VARCHAR2(500);
        l_req       UTL_HTTP.REQ;
        l_resp      UTL_HTTP.RESP;
        l_body      VARCHAR2(4000);
        l_buffer    VARCHAR2(32767);
        l_response  VARCHAR2(32767);
        l_token     VARCHAR2(4000);
        l_role      VARCHAR2(50);
        l_fullname  VARCHAR2(200);
    BEGIN
        -- Leer URL desde configuración centralizada
        l_api_base := get_config('API_BASE_URL');

        -- Construir el body JSON con UPPER() en el username para comparación
        -- case-insensitive en SP_USER_GET_BY_USERNAME
        l_body := '{"username":' || apex_json.stringify(UPPER(p_username))
               || ',"password":' || apex_json.stringify(p_password) || '}';

        l_req := UTL_HTTP.BEGIN_REQUEST(
            url    => l_api_base || '/api/auth/login',
            method => 'POST'
        );
        UTL_HTTP.SET_HEADER(l_req, 'Content-Type',  'application/json');
        UTL_HTTP.SET_HEADER(l_req, 'Content-Length', LENGTHB(l_body));
        UTL_HTTP.WRITE_TEXT(l_req, l_body);

        l_resp := UTL_HTTP.GET_RESPONSE(l_req);

        BEGIN
            LOOP
                UTL_HTTP.READ_TEXT(l_resp, l_buffer, 32767);
                l_response := l_response || l_buffer;
            END LOOP;
        EXCEPTION
            WHEN UTL_HTTP.END_OF_BODY THEN NULL;
        END;

        UTL_HTTP.END_RESPONSE(l_resp);

        IF l_resp.status_code != 200 THEN
            apex_debug.error('Login fallido — Status: %s', l_resp.status_code);
            RETURN FALSE;
        END IF;

        apex_json.parse(l_response);
        l_token    := apex_json.get_varchar2(p_path => 'token');
        l_role     := apex_json.get_varchar2(p_path => 'role');
        l_fullname := apex_json.get_varchar2(p_path => 'fullName');

        IF l_token IS NULL THEN
            apex_debug.error('Sin token: %s', l_response);
            RETURN FALSE;
        END IF;

        apex_util.set_session_state('G_JWT_TOKEN',    l_token);
        apex_util.set_session_state('G_USER_ROLE',    l_role);
        apex_util.set_session_state('G_USER_FULLNAME', l_fullname);

        RETURN TRUE;

    EXCEPTION
        WHEN OTHERS THEN
            apex_debug.error('Excepción en authenticate_user: %s', SQLERRM);
            RETURN FALSE;
    END authenticate_user;

END pkg_auth_anonimizador;
/

-- =============================================
-- Verificar — dos filas VALID
-- =============================================
SELECT object_name, object_type, status
FROM   user_objects
WHERE  object_name = 'PKG_AUTH_ANONIMIZADOR'
ORDER BY object_type;

-- =============================================
-- TEST DE CONECTIVIDAD
-- Reemplazar TuPassword antes de ejecutar.
-- =============================================
/*
SET SERVEROUTPUT ON
DECLARE
    l_request  UTL_HTTP.REQ;
    l_response UTL_HTTP.RESP;
    l_text     VARCHAR2(32767);
    l_body     VARCHAR2(200) := '{"username":"ADMIN","password":"CGRT3ST"}';
    l_api_base VARCHAR2(500);
BEGIN
    SELECT config_value INTO l_api_base
    FROM   anonimizador_config
    WHERE  config_key = 'API_BASE_URL';

    l_request := UTL_HTTP.BEGIN_REQUEST(
        url    => l_api_base || '/api/auth/login',
        method => 'POST'
    );
    UTL_HTTP.SET_HEADER(l_request, 'Content-Type',  'application/json');
    UTL_HTTP.SET_HEADER(l_request, 'Content-Length', LENGTH(l_body));
    UTL_HTTP.WRITE_TEXT(l_request, l_body);

    l_response := UTL_HTTP.GET_RESPONSE(l_request);
    DBMS_OUTPUT.PUT_LINE('Status: ' || l_response.status_code);

    UTL_HTTP.READ_TEXT(l_response, l_text, 32767);
    DBMS_OUTPUT.PUT_LINE('Response: ' || SUBSTR(l_text, 1, 500));

    UTL_HTTP.END_RESPONSE(l_response);
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error: '   || SQLERRM);
        DBMS_OUTPUT.PUT_LINE('Detalle: ' || UTL_HTTP.GET_DETAILED_SQLERRM);
END;
/
*/