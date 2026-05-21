namespace Anonimizador___API.Application.Common
{
    /// <summary>
    /// Represents a PDF word with coordinates.
    /// </summary>
    public class PdfWordInfo
    {
        public int PageNumber { get; set; }

        public string Text { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }
}