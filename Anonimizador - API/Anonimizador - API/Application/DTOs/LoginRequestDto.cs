namespace Anonimizador___API.Application.DTOs
{
    /// <summary>
    /// DTO para la solicitud de login.
    /// </summary>
    public class LoginRequestDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}