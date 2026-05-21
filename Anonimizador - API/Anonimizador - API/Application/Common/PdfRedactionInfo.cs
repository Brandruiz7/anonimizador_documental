namespace Anonimizador___API.Application.Common
{
    /// <summary>
    /// Representa un área de redacción dentro de un PDF.
    /// Contiene las coordenadas y el texto de reemplazo para aplicar sobre la imagen.
    /// </summary>
    public class PdfRedactionInfo
    {
        /// <summary>Número de página donde se aplica la redacción (base 1).</summary>
        public int PageNumber { get; set; }

        /// <summary>Texto original que será redactado.</summary>
        public string OriginalText { get; set; } = string.Empty;

        /// <summary>Etiqueta de reemplazo que se mostrará en el área redactada.</summary>
        public string ReplacementText { get; set; } = string.Empty;

        /// <summary>Coordenada X del extremo izquierdo del área (en puntos PDF).</summary>
        public double X { get; set; }

        /// <summary>Coordenada Y del extremo inferior del área (en puntos PDF).</summary>
        public double Y { get; set; }

        /// <summary>Ancho del área de redacción (en puntos PDF).</summary>
        public double Width { get; set; }

        /// <summary>Alto del área de redacción (en puntos PDF).</summary>
        public double Height { get; set; }
    }
}