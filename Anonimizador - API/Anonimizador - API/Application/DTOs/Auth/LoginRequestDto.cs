namespace Anonimizador___API.Application.DTOs.Auth
{
    /// <summary>
    /// DTO para la solicitud de inicio de sesión.
    /// </summary>
    public class LoginRequestDto
    {
        /// <summary>Nombre de usuario.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Contraseña en texto plano — se valida contra el hash en BD.</summary>
        public string Password { get; set; } = string.Empty;
    }
}