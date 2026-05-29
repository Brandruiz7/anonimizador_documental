using Anonimizador___API.Application.DTOs.Auth;

namespace Anonimizador___API.Interfaces.Services
{
    /// <summary>
    /// Contrato para el servicio de autenticación y generación de tokens JWT.
    /// Su implementación concreta es <see cref="Services.Auth.AuthService"/>.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Valida las credenciales del usuario contra la BD y genera un token JWT firmado.
        /// </summary>
        /// <param name="request">Credenciales de acceso (usuario y contraseña).</param>
        /// <returns>Token JWT, datos del usuario autenticado y fecha de expiración.</returns>
        /// <exception cref="UnauthorizedAccessException">
        /// Si las credenciales son inválidas o el usuario está inactivo.
        /// </exception>
        Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
    }
}