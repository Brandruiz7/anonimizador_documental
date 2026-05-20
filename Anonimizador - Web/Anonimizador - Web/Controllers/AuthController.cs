using Anonimizador___Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Anonimizador___Web.Controllers
{
    [Route("auth")]
    public class AuthController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("login")]
        public IActionResult Login(string? returnUrl = null)
        {
            // Si ya está autenticado, redirige al inicio
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var apiUrl = _configuration["ApiSettings:BaseUrl"];

                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                using var client = new HttpClient(handler);

                var body = JsonSerializer.Serialize(new
                {
                    username = model.Username,
                    password = model.Password
                });

                var response = await client.PostAsync(
                    $"{apiUrl}/api/auth/login",
                    new StringContent(body, Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    ModelState.AddModelError("", "Usuario o contraseña incorrectos");
                    return View(model);
                }

                var json = await response.Content.ReadAsStringAsync();
                var loginResponse = JsonSerializer.Deserialize<JsonElement>(json);

                var token = loginResponse.GetProperty("token").GetString()!;
                var username = loginResponse.GetProperty("username").GetString()!;
                var role = loginResponse.GetProperty("role").GetString()!;

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name,  username),
                    new Claim(ClaimTypes.Role,  role),
                    new Claim("jwt_token",      token)
                };

                var identity = new ClaimsIdentity(
                    claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = false, // ← se elimina al cerrar el navegador
                        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                    });

                _logger.LogInformation("User {Username} logged in", username);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error");
                ModelState.AddModelError("", "Error de conexión con el servicio");
                return View(model);
            }
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Login");
        }

        /// <summary>
        /// Renueva la sesión del usuario si está autenticado.
        /// </summary>
        [HttpPost("renew")]
        [Authorize]
        public IActionResult Renew()
        {
            // El SlidingExpiration en la cookie ya renueva automáticamente
            // con cada request — este endpoint solo sirve para disparar ese mecanismo
            return Ok();
        }
    }
}
