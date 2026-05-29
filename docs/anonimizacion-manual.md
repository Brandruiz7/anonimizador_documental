# ✍️ Anonimización manual

[← Volver al README principal](../README.md)

## ¿Qué es el modo manual?

En el modo manual el usuario ingresa directamente los datos sensibles que desea anonimizar. No hay análisis previo del documento — el sistema simplemente busca y reemplaza exactamente los valores indicados.

---

## Flujo completo

```text
1. Cliente envía POST /api/documents/upload (multipart/form-data)
   ├── File: archivo .docx o .pdf
   ├── GeneralData.CaseNumber: número de expediente (opcional)
   ├── GeneralData.OfficeNumber: número de oficio (opcional)
   ├── Persons[0].FullName: nombre completo
   ├── Persons[0].Identification: cédula
   ├── Persons[0].NameVariations[0]: variación del nombre
   └── ... (más personas y campos)

2. DocumentsController.Upload() recibe el request
3. DocumentService.UploadStreamAsync() orquesta el proceso:
   a. Valida el archivo (extensión, MIME, tamaño, estructura)
   b. Calcula hash SHA256 del original
   c. Registra proceso en BD con estado PROCESSING
   d. BuildAnonymizationTargets() construye la lista de targets
   e. Selecciona WordDocumentProcessor o PdfDocumentProcessor
   f. ProcessAsync() aplica los reemplazos en memoria
   g. Registra versión ANONYMIZED y auditoría en BD
   h. Actualiza estado a ANONYMIZED
   i. Retorna stream del documento anonimizado
```

---

## Construcción de targets

`BuildAnonymizationTargets()` en `DocumentService` transforma el request en una lista plana de `AnonymizationTargetDto`:

```text
PersonIndex = -1  → datos generales (CaseNumber, OfficeNumber)
PersonIndex =  0  → Persona 1 (FullName, Identification, Email, ...)
PersonIndex =  0  → variación de nombre de Persona 1
PersonIndex =  0  → variación de cédula de Persona 1
PersonIndex =  1  → Persona 2
...
```

Las personas sin ningún campo con valor se omiten silenciosamente.

---

## Etiquetas generadas

| Campo | Etiqueta | Clasificación |
|---|---|---|
| Nombre completo | `[Px-Nombre]` | Personal |
| Identificación | `[Px-Cédula]` | Personal |
| Correo electrónico | `[Px-Correo]` | Personal |
| Teléfono | `[Px-Tel]` | Personal |
| Cargo o puesto | `[Px-Cargo]` | Personal |
| Dirección | `[Px-Dir]` | Personal |
| Institución | `[Px-Institución]` | Personal |
| Cuenta bancaria | `[Px-CuentaBancaria]` | Sensible (PRODHAB) |
| Condición médica | `[Px-CondiciónMédica]` | Sensible (PRODHAB) |
| Texto libre | `[Px-Dato]` | Libre |
| Número de expediente | `[Expediente]` | General |
| Número de oficio | `[N° Oficio]` | General |

Donde `x` es el índice de persona base 1 (P1, P2, P3...).

---

## Motor de reemplazo — TextAnonymizationEngine

`TextAnonymizationEngine.ApplyTargets()` aplica los reemplazos sobre texto plano:

- Los reemplazos son **case-insensitive** — "Juan" y "JUAN" producen el mismo resultado
- Los reemplazos son **acumulativos** — el texto resultante de un reemplazo es la entrada del siguiente
- La auditoría se registra **una sola vez por valor único** aunque aparezca múltiples veces
- Las variaciones se procesan como targets adicionales con la misma etiqueta que el campo principal

---

## Variaciones

El sistema soporta variaciones para nombre, cédula y teléfono. Por ejemplo:

```
FullName: "Juan Carlos Pérez Rodríguez"
NameVariations: ["Pérez Rodríguez", "Juan Pérez"]
```

Todas las variaciones se reemplazan con la misma etiqueta `[P1-Nombre]`.

---

## Ver también

- [Anonimización con IA](anonimizacion-ia.md)
- [Procesamiento DOCX](procesamiento-docx.md)
- [Procesamiento PDF](procesamiento-pdf.md)