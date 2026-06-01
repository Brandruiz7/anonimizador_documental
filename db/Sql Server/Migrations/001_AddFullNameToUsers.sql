/*****************************************************************************************
 Migración  : 001_AddFullNameToUsers
 Descripción: Agrega el campo FullName a la tabla USERS para mostrar
              el nombre completo del usuario en la interfaz.
 Autor      : Ruiz
 Fecha      : 2026-05-21
 Integrado  : ✅ Integrado
*****************************************************************************************/

USE DocumentAnonymizerDB;
GO

-- =============================================
-- CAMPO: Agregar FullName a USERS
-- Verificación de idempotencia — se puede
-- ejecutar múltiples veces sin romper nada
-- =============================================
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME  = 'USERS'
      AND COLUMN_NAME = 'FullName'
)
BEGIN
    ALTER TABLE USERS ADD FullName NVARCHAR(200) NULL;
    PRINT 'Columna FullName agregada a USERS.';
END
ELSE
BEGIN
    PRINT 'La columna FullName ya existe — migración omitida.';
END
GO

-- =============================================
-- DATOS: Actualizar usuarios existentes
-- Solo actualiza si el campo está vacío
-- =============================================
UPDATE USERS
SET FullName = 'Brandon José Ruiz Miranda'
WHERE Username = 'admin'
  AND FullName IS NULL;
GO

-- =============================================
-- SP: SP_USER_GET_BY_USERNAME
-- Incluye FullName en el resultado
-- =============================================
CREATE OR ALTER PROCEDURE SP_USER_GET_BY_USERNAME
    @Username NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.Username,
        u.PasswordHash,
        u.IsActive,
        u.FullName,
        r.RoleName
    FROM USERS u
    INNER JOIN ROLES r ON u.RoleId = r.RoleId
    WHERE u.Username = @Username;
END;
GO