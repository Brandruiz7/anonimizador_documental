namespace Anonimizador___API.Application.Common
{
    /// <summary>
    /// Representa una línea de texto reconstruida desde un PDF.
    /// Agrupa palabras que pertenecen a la misma línea por posición vertical.
    /// </summary>
    public class PdfLineInfo
    {
        /// <summary>Número de página donde aparece la línea (base 1).</summary>
        public int PageNumber { get; set; }

        /// <summary>Texto completo de la línea, reconstruido uniendo las palabras.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Lista de palabras individuales que componen la línea.</summary>
        public List<PdfWordInfo> Words { get; set; } = new();
    }
}