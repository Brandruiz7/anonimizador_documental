using System.ComponentModel.DataAnnotations;

namespace Anonimizador___API.Application.DTOs.Auth
{
    /// <summary>
    /// DTO para la solicitud de inicio de sesión.
    /// Los atributos de validación son evaluados por ModelState en AuthController
    /// antes de llegar al servicio.
    /// </summary>
    public class LoginRequestDto
    {
        /// <summary>
        /// Nombre de usuario. Requerido, máximo 100 caracteres.
        /// </summary>
        [Required(ErrorMessage = "El nombre de usuario es requerido.")]
        [MaxLength(100, ErrorMessage = "El nombre de usuario no puede superar 100 caracteres.")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Contraseña en texto plano — se valida contra el hash BCrypt almacenado en BD.
        /// Nunca se almacena ni se loguea.
        /// </summary>
        [Required(ErrorMessage = "La contraseña es requerida.")]
        [MaxLength(256, ErrorMessage = "La contraseña no puede superar 256 caracteres.")]
        public string Password { get; set; } = string.Empty;
    }
}