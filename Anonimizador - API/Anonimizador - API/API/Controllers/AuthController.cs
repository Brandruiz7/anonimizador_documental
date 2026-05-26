using Anonimizador___API.Application.DTOs.Auth;
using Anonimizador___API.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Anonimizador___API.API.Controllers
{
    /// <summary>
    /// Controlador de autenticación.
    /// Expone el endpoint de login y generación de tokens JWT.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        /// <summary>
        /// Inicializa el controlador con el servicio de autenticación.
        /// </summary>
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Autentica un usuario y retorna un token JWT.
        /// </summary>
        /// <param name="request">Credenciales de acceso (usuario y contraseña).</param>
        /// <returns>Token JWT y datos del usuario autenticado.</returns>
        /// <response code="200">Login exitoso.</response>
        /// <response code="401">Credenciales inválidas.</response>
        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
    }
}