using System.Text.RegularExpressions;

namespace Anonimizador___API.Application.Common
{
    /// <summary>
    /// Centralized regex catalog for anonymization rules.
    /// </summary>
    public static class RegexCatalog
    {
        /// <summary>
        /// Full name detection pattern.
        /// </summary>
        public static readonly Regex FullName =
            new(
                @"\b[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+ [A-ZÁÉÍÓÚÑ][a-záéíóúñ]+\b",
                RegexOptions.Compiled);

        /// <summary>
        /// Costa Rica identification number pattern.
        /// </summary>
        public static readonly Regex CostaRicaId =
            new(
                @"\b\d{1}-\d{4}-\d{4}\b",
                RegexOptions.Compiled);

        /// <summary>
        /// Email detection pattern.
        /// </summary>
        public static readonly Regex Email =
            new(
                @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
                RegexOptions.Compiled);

        /// <summary>
        /// Costa Rica phone number pattern.
        /// </summary>
        public static readonly Regex Phone =
            new(
                @"\b\d{4}-\d{4}\b",
                RegexOptions.Compiled);
    }
}