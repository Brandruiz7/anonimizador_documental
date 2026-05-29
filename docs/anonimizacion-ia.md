# 🤖 Anonimización con IA

[← Volver al README principal](../README.md)

## ¿Qué es el modo IA?

En el modo IA el sistema analiza el documento automáticamente antes de que el usuario confirme los datos. Combina dos motores de detección: **Regex** para patrones exactos e **IA semántica** para entidades nombradas en contexto.

---

## Flujo completo

```text
1. Cliente envía POST /api/documents/analyze (multipart/form-data)
   ├── File: archivo .docx o .pdf
   └── AdditionalContext: contexto adicional opcional

2. DocumentsController.Analyze() → DocumentAnalysisService.AnalyzeAsync()

3. Extracción de texto:
   ├── DOCX → OpenXML SDK, recorre todos los Text del Body
   └── PDF  → PdfPig, agrupa palabras por línea según posición Y

4. Detección paralela:
   ├── ApplyRegexDetection() → RegexCatalog (cédulas, correos, teléfonos, nombres)
   └── ApplyAiDetectionAsync() → prompt estructurado → Ollama o Gemini

5. MergeResults() fusiona ambos resultados complementando campos faltantes

6. Retorna DocumentAnalysisResultDto al cliente para revisión

7. Usuario revisa, corrige y confirma → POST /api/documents/upload (modo manual)
```

> El análisis **no modifica ni guarda** el documento — es solo detección para revisión.

---

## Motor Regex — RegexCatalog

Detecta patrones exactos compilados al iniciar la aplicación:

| Regex | Patrón | Ejemplo |
|---|---|---|
| `CostaRicaId` | `\d{1}-\d{4}-\d{4}` | 1-2345-6789 |
| `Email` | estándar RFC 5321 | juan@correo.com |
| `Phone` | `\d{4}-\d{4}` | 8888-8888 |
| `FullName` | dos palabras capitalizadas | Juan Pérez |

**Limitaciones:** el Regex de nombre puede generar falsos positivos con instituciones o inicios de oración. La IA complementa estos casos.

---

## Motor IA — Prompt estructurado

El prompt instruye al modelo a retornar bloques con formato fijo:

```
---PERSONA---
NOMBRE: Juan Carlos Pérez Rodríguez
CEDULA: 1-2345-6789
EMAIL: juan@correo.com
TELEFONO: NONE
CARGO: Director Ejecutivo
VARIACIONES: Pérez Rodríguez, Juan Pérez
---FIN---

---EXTRA---
TIPO: BANK_ACCOUNT
VALOR: CR21-0152-0001-0026-3298-66
---FIN---
```

Este formato estructurado es más robusto que JSON libre con modelos como Mistral, que tienden a agregar texto fuera del JSON o a cambiar la estructura.

El texto del documento se **trunca a 4000 caracteres** antes de enviarse al modelo para evitar superar el contexto máximo.

---

## Parseo de respuesta — ParseStructuredResponse

`ParseStructuredResponse()` procesa la respuesta línea por línea:

```text
1. Detecta ---PERSONA--- → inicia nuevo DetectedPersonDto
2. Lee pares CLAVE: valor y asigna al campo correspondiente
3. VARIACIONES se parsea separando por coma y filtrando:
   - Fragmentos menores a 3 caracteres
   - Valores "NONE"
   - Nombres de empresas (S.A., S.R.L., Ltda)
4. Detecta ---EXTRA--- → inicia datos adicionales
5. ---FIN--- → cierra el bloque actual
```

---

## Fusión de resultados — MergeResults y MergePersons

`MergeResults()` combina Regex e IA:
- Las personas detectadas por IA son la base
- Los campos de Regex **complementan** campos faltantes en la primera persona IA
- No sobreescribe valores ya detectados por IA

`MergePersons()` evita duplicados dentro del resultado IA:
- Si dos personas comparten palabras del nombre (más de 2 palabras largas), se fusionan
- El nombre de la persona duplicada se agrega como variación de la principal

---

## Motores de IA disponibles

### Ollama / Mistral (activo por defecto)
- Corre localmente — los documentos no salen del servidor
- Requiere instalación de Ollama y descarga del modelo
- Configurado en `appsettings.json` bajo la sección `Ollama`

### Google Gemini (alternativa para la nube)
- API externa de Google — los documentos se envían a Google
- Requiere API Key con cuota activa en Google Cloud Console
- Para activar: ver [Configuración](configuracion.md)

---

## Ver también

- [Anonimización manual](anonimizacion-manual.md)
- [Configuración de motores de IA](configuracion.md)