# 📝 Procesamiento DOCX

[← Volver al README principal](../README.md)

## Enfoque: reemplazo de texto en XML

El procesador DOCX (`WordDocumentProcessor`) trabaja directamente sobre el XML interno del archivo `.docx` mediante **OpenXML SDK**. Reemplaza el texto sensible por etiquetas neutrales preservando el formato original del documento.

---

## Zonas cubiertas

| Zona | Descripción |
|---|---|
| ✅ Párrafos del cuerpo | Texto principal del documento |
| ✅ Tablas | Celdas en todas las tablas |
| ✅ Encabezados (Headers) | Texto en encabezados de todas las secciones |
| ✅ Pies de página (Footers) | Texto en pies de página de todas las secciones |
| ✅ Textboxes VML | Cuadros de texto en formato VML (Word antiguo) |
| ✅ Textboxes Drawing | Cuadros de texto en formato Drawing (Word moderno) |
| ✅ Cambios rastreados (ins/del) | Texto en marcas de revisión Track Changes |

---

## Pipeline completo

```text
1. El documento se carga en MemoryStream — nunca toca disco
2. Se abre con WordprocessingDocument.Open() en modo edición
3. Para cada zona del documento:
   a. Se recorren todos los Run (fragmentos de texto con formato)
   b. AnonymizeRun() aplica los reemplazos en cada Run
4. Se guarda el documento modificado en memoria
5. Se retorna el stream del resultado con auditoría
```

---

## Problema del fragmentado de Runs

Word divide el texto en múltiples `Run` por razones de formato, corrección ortográfica o historial de edición. Por ejemplo:

```xml
<!-- "Juan Pérez" puede estar fragmentado así en el XML: -->
<w:r><w:t>Ju</w:t></w:r>
<w:r><w:t>an </w:t></w:r>
<w:r><w:t>Pér</w:t></w:r>
<w:r><w:t>ez</w:t></w:r>
```

Para resolver esto, el procesador:
1. **Consolida** todos los Runs de un párrafo en texto plano
2. **Aplica** los reemplazos sobre el texto consolidado
3. **Reconstruye** el párrafo asignando el texto resultante al primer Run y limpiando los demás

---

## Motor de reemplazo — TextAnonymizationEngine

`TextAnonymizationEngine.ApplyTargets()` se invoca en cada fragmento de texto:

- Reemplazos **case-insensitive** — "JUAN PÉREZ" y "juan pérez" producen `[P1-Nombre]`
- Reemplazos **acumulativos** — si el nombre aparece en múltiples párrafos, todos se reemplazan
- Auditoría registrada **una vez por valor único** aunque aparezca múltiples veces

---

## Cambios rastreados (Track Changes)

Los documentos con Track Changes activo contienen texto en elementos `<w:ins>` (inserciones) y `<w:del>` (eliminaciones). El procesador también recorre estos elementos para garantizar que el texto sensible no quede expuesto en el historial de cambios.

---

## Ver también

- [Procesamiento PDF](procesamiento-pdf.md)
- [Anonimización manual](anonimizacion-manual.md)