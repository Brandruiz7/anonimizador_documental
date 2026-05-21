using Anonimizador___API.Application.DTOs.Auth;

namespace Anonimizador___API.Interfaces.Services
{
    /// <summary>
    /// Contrato para el servicio de autenticación y generación de tokens JWT.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Valida las credenciales del usuario y genera un token JWT.
        /// </summary>
        /// <param name="request">Credenciales de acceso (usuario y contraseña).</param>
        /// <returns>Token JWT y datos del usuario autenticado.</returns>
        Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
    }
}