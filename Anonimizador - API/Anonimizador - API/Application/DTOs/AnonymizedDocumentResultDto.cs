namespace Anonimizador___API.Application.DTOs
{
    /// <summary>
    /// Represents anonymized document result data.
    /// </summary>
    public class AnonymizedDocumentResultDto
    {
        /// <summary>
        /// Gets or sets anonymized file bytes.
        /// </summary>
        public byte[] FileBytes { get; set; } = [];

        /// <summary>
        /// Gets or sets generated file name.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets content type.
        /// </summary>
        public string ContentType { get; set; } = string.Empty;
    }
}