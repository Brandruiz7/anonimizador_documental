namespace Anonimizador___API.Application.DTOs.Auth
{
    /// <summary>
    /// Representa un usuario recuperado desde la base de datos.
    /// </summary>
    public class UserDto
    {
        /// <summary>Identificador único del usuario.</summary>
        public int UserId { get; set; }

        /// <summary>Nombre de la credencial del usuario.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Hash BCrypt de la contraseña.</summary>
        public string PasswordHash { get; set; } = string.Empty;
        
        /// <summary> Nombre completo del usuario. </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>Nombre del rol asignado.</summary>
        public string RoleName { get; set; } = string.Empty;

        /// <summary>Indica si el usuario está activo en el sistema.</summary>
        public bool IsActive { get; set; }
    }
}