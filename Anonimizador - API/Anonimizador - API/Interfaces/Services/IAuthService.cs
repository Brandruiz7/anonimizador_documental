using Anonimizador___API.Application.DTOs;

namespace Anonimizador___API.Interfaces.Services
{
    /// <summary>
    /// Contrato para autenticación y generación de tokens.
    /// </summary>
    public interface IAuthService
    {
        Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
    }
}