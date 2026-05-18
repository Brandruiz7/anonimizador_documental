namespace Anonimizador___API.Application.DTOs
{
    /// <summary>
    /// DTO con el token JWT generado tras login exitoso.
    /// </summary>
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}