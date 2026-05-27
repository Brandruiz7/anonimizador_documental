/*****************************************************************************************
 Migración  : 002_ExpandAnonymizationFields
 Descripción: Expande los campos de anonimización por persona agregando
              datos sensibles (cuenta bancaria, condición médica, institución
              y texto libre) y campos generales del documento
              (número de expediente y número de oficio).
 Autor      : Ruiz
 Fecha      : 2026-05-21
 Integrado  : ✅ Integrado
*****************************************************************************************/

USE DocumentAnonymizerDB;
GO

-- =============================================
-- No se requieren cambios en tablas existentes.
-- Los nuevos campos se manejan a nivel de
-- aplicación en los DTOs y el motor de
-- anonimización — la tabla ANONYMIZED_FIELDS
-- ya soporta cualquier FieldType como string.
-- =============================================
PRINT 'Migración 002 — sin cambios en esquema de BD.';
PRINT 'Los nuevos campos se gestionan a nivel de aplicación.';
GO