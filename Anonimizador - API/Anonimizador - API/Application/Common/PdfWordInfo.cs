namespace Anonimizador___API.Application.Common
{
    /// <summary>
    /// Representa una palabra extraída de un PDF con sus coordenadas exactas.
    /// Utilizada para localizar y redactar datos sensibles sobre la imagen renderizada.
    /// </summary>
    public class PdfWordInfo
    {
        /// <summary>Número de página donde aparece la palabra (base 1).</summary>
        public int PageNumber { get; set; }

        /// <summary>Texto de la palabra.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Coordenada X del extremo izquierdo (en puntos PDF).</summary>
        public double X { get; set; }

        /// <summary>Coordenada Y del extremo inferior (en puntos PDF).</summary>
        public double Y { get; set; }

        /// <summary>Ancho de la palabra (en puntos PDF).</summary>
        public double Width { get; set; }

        /// <summary>Alto de la palabra (en puntos PDF).</summary>
        public double Height { get; set; }
    }
}