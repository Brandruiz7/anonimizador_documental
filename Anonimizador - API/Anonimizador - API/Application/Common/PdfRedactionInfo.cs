namespace Anonimizador___API.Application.Common
{
    /// <summary>
    /// Represents a redaction area inside PDF.
    /// </summary>
    public class PdfRedactionInfo
    {
        public int PageNumber { get; set; }

        public string OriginalText { get; set; } = string.Empty;

        public string ReplacementText { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }
}