namespace Anonimizador___API.Application.DTOs.Auth
{
    /// <summary>
    /// DTO con el token JWT generado tras un login exitoso.
    /// </summary>
    public class LoginResponseDto
    {
        /// <summary>Token JWT firmado.</summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>Nombre de credencial del usuario autenticado.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Nombre completo del usuario.</summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>Rol asignado al usuario.</summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>Fecha y hora de expiración del token.</summary>
        public DateTime ExpiresAt { get; set; }
    }
}