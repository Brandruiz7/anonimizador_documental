using Anonimizador___API.Application.DTOs;
using Anonimizador___API.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Anonimizador___API.API.Controllers
{
    /// <summary>
    /// Controlador de autenticación.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Autentica un usuario y retorna un JWT.
        /// </summary>
        /// <response code="200">Login exitoso, retorna token</response>
        /// <response code="401">Credenciales inválidas</response>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
    }
}