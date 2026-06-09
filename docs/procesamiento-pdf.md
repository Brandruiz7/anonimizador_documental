# 🖨️ Procesamiento PDF

[← Volver al README principal](../README.md)

## Enfoque: redacción por imagen

El procesador PDF (`PdfDocumentProcessor`) usa un enfoque de **redacción por imagen** en lugar de modificar la capa de texto del PDF. Esto garantiza que los datos originales no sean recuperables — no existe una capa de texto seleccionable en el documento resultante.

---

## Pipeline completo

```text
1. PdfPig identifica las palabras y sus coordenadas en el PDF original
2. BuildRedactions() busca los valores sensibles y construye rectángulos de redacción
3. PDFtoImage renderiza cada página a bitmap a 250 DPI
4. Para cada página con redacciones:
   a. Se dibuja un rectángulo de fondo (#f2f4ff) sobre las palabras sensibles
   b. Se dibuja la etiqueta de reemplazo centrada en el rectángulo
5. PdfSharp reconstruye el PDF desde las imágenes redactadas
6. El PDF resultante no tiene capa de texto — solo imágenes
```

---

## Sistema de coordenadas

PDF y bitmap usan sistemas de coordenadas distintos:

```text
PDF:    origen en la esquina INFERIOR-IZQUIERDA, Y crece hacia arriba
Bitmap: origen en la esquina SUPERIOR-IZQUIERDA, Y crece hacia abajo
```

La conversión se aplica en cada redacción:

```csharp
var pixelX = redaction.X * scaleX;
var pixelY = (pdfHeight - redaction.Y - redaction.Height) * scaleY;
```

Los factores de escala convierten puntos PDF a píxeles del bitmap según el DPI:

```csharp
var scaleX = bitmap.Width  / pdfWidth;
var scaleY = bitmap.Height / pdfHeight;
```

---

## Búsqueda de palabras — tres niveles

`BuildRedactions()` aplica búsqueda en tres niveles para máxima cobertura:

### Nivel 1 — Líneas agrupadas
Busca la frase exacta dentro de líneas agrupadas por posición Y (tolerancia 2 puntos). Cubre el caso más común donde el texto está en una sola línea.

### Nivel 2 — Palabras globales de la página
Si no encuentra en líneas, busca en todas las palabras de la página. Esto cubre texto en negrita que PdfPig agrupa de forma separada de las palabras normales.

### Nivel 3 — Palabra individual (solo nombres)
Si el nombre tiene espacios y no fue encontrado completo, busca cada palabra del nombre individualmente. Cubre casos donde el nombre está fragmentado en líneas distintas.

---

## Búsqueda elástica — FindWordsForPhrase

`FindWordsForPhrase()` usa una **ventana deslizante** para encontrar frases divididas entre palabras contiguas:

```text
Frase buscada: "1-2345-6789"
Palabras en PDF: ["1-", "2345-", "6789"]  ← separadas por el renderizado
```

La normalización elimina guiones y espacios antes de comparar, permitiendo encontrar el valor aunque esté fragmentado.

---

## Fusión de redacciones adyacentes

`MergeAdjacentRedactions()` fusiona rectángulos que corresponden al mismo dato:

- Misma página
- Misma etiqueta de reemplazo
- Misma línea (diferencia de Y ≤ 10 puntos)
- Distancia horizontal ≤ 50 puntos

Esto evita múltiples rectángulos pequeños separados para un mismo dato.

---

## Diseño visual de las redacciones

Cada redacción tiene:
- **Fondo**: color corporativo `#f2f4ff` (azul muy claro)
- **Etiqueta**: color `#36499b` (azul institucional), negrita, centrada
- **Margen**: 4px alrededor del texto para mayor legibilidad
- **Tamaño de fuente**: entre 10px y 14px, proporcional al alto del rectángulo

---

## Resolución y calidad

- Renderizado a **250 DPI** — suficiente para legibilidad, equilibrado con tamaño del archivo
- Imágenes guardadas en **PNG con calidad 95** antes de insertar en el PDF
- Factor de conversión DPI→puntos PDF: `píxeles * 72 / 250`

---

## Soporte para PDFs con campos AcroForm

Los formularios institucionales (INS, CCSS, CGR) usan campos AcroForm
interactivos cuyos valores no están en la capa de texto estático del PDF.

El sistema los maneja en dos niveles:

- **Análisis IA** (`ExtractTextFromPdf`): lee los campos con `TryGetForm`
  y los concatena al texto extraído antes de enviarlo a Mistral.
- **Redacción** (`BuildRedactions`): convierte los campos AcroForm en
  `PdfWordInfo` con sus coordenadas reales mediante `ExtractAcroFormWords`,
  integrándolos al pool de palabras antes de buscar los valores a redactar.

---

## Ver también

- [Procesamiento DOCX](procesamiento-docx.md)
- [Anonimización manual](anonimizacion-manual.md)