namespace Anonimizador___API.Application.DTOs
{
    /// <summary>
    /// Representa un usuario recuperado de la base de datos.
    /// </summary>
    public class UserDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}