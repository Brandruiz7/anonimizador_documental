using System.ComponentModel.DataAnnotations;

namespace Anonimizador___Web.Models
{
    /// <summary>
    /// ViewModel para el formulario de inicio de sesión.
    /// </summary>
    public class LoginViewModel
    {
        /// <summary>Nombre de usuario.</summary>
        [Required(ErrorMessage = "El usuario es requerido.")]
        public string Username { get; set; } = string.Empty;

        /// <summary>Contraseña del usuario.</summary>
        [Required(ErrorMessage = "La contraseña es requerida.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}