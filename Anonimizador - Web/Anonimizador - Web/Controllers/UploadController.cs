using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anonimizador___Web.Controllers
{
    /// <summary>
    /// Controlador que actúa como proxy entre el wizard del Web y el API.
    /// Recibe el formulario multipart, reenvía los datos al API
    /// y retorna el documento anonimizado al cliente.
    /// </summary>
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Route("upload")]
    public class UploadController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<UploadController> _logger;

        /// <summary>
        /// Inicializa el controlador con sus dependencias.
        /// </summary>
        public UploadController(
            IConfiguration configuration,
            ILogger<UploadController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Recibe el formulario del wizard, reenvía los datos al API
        /// y retorna el documento anonimizado para descarga.
        /// </summary>
        [HttpPost("process")]
        [IgnoreAntiforgeryToken]
        [RequestSizeLimit(104857600)]
        [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
        public async Task<IActionResult> Process()
        {
            try
            {
                var form = await Request.ReadFormAsync();
                var file = form.Files.GetFile("documento");

                if (file == null || file.Length == 0)
                    return BadRequest("No se recibió ningún archivo.");

                var apiUrl = _configuration["ApiSettings:BaseUrl"];
                var token = User.FindFirst("jwt_token")?.Value
                    ?? _configuration["ApiSettings:Token"];

                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                using var content = new MultipartFormDataContent();

                var fileContent = new StreamContent(file.OpenReadStream());
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                content.Add(fileContent, "File", file.FileName);

                var uploadedBy = User.FindFirst(
                    System.Security.Claims.ClaimTypes.Name)?.Value
                    ?? form["UploadedBy"].ToString()
                    ?? "unknown";
                content.Add(new StringContent(uploadedBy), "UploadedBy");

                var personIndex = 0;
                while (form.ContainsKey($"Persons[{personIndex}].FullName"))
                {
                    var prefix = $"Persons[{personIndex}]";

                    content.Add(new StringContent(form[$"{prefix}.FullName"].ToString() ?? ""), $"Persons[{personIndex}].FullName");
                    content.Add(new StringContent(form[$"{prefix}.Identification"].ToString() ?? ""), $"Persons[{personIndex}].Identification");
                    content.Add(new StringContent(form[$"{prefix}.Email"].ToString() ?? ""), $"Persons[{personIndex}].Email");
                    content.Add(new StringContent(form[$"{prefix}.PhoneNumber"].ToString() ?? ""), $"Persons[{personIndex}].PhoneNumber");
                    content.Add(new StringContent(form[$"{prefix}.Position"].ToString() ?? ""), $"Persons[{personIndex}].Position");
                    content.Add(new StringContent(form[$"{prefix}.Address"].ToString() ?? ""), $"Persons[{personIndex}].Address");
                    content.Add(new StringContent(form[$"{prefix}.Institution"].ToString() ?? ""), $"Persons[{personIndex}].Institution");
                    content.Add(new StringContent(form[$"{prefix}.BankAccount"].ToString() ?? ""), $"Persons[{personIndex}].BankAccount");
                    content.Add(new StringContent(form[$"{prefix}.MedicalCondition"].ToString() ?? ""), $"Persons[{personIndex}].MedicalCondition");
                    content.Add(new StringContent(form[$"{prefix}.FreeText"].ToString() ?? ""), $"Persons[{personIndex}].FreeText");

                    // Variaciones del nombre
                    var variationIndex = 0;
                    while (form.ContainsKey($"Persons[{personIndex}].NameVariations[{variationIndex}]"))
                    {
                        var variation = form[$"Persons[{personIndex}].NameVariations[{variationIndex}]"].ToString();
                        if (!string.IsNullOrWhiteSpace(variation))
                            content.Add(new StringContent(variation),
                                $"Persons[{personIndex}].NameVariations[{variationIndex}]");
                        variationIndex++;
                    }

                    // Variaciones de cédula
                    var idVarIndex = 0;
                    while (form.ContainsKey($"Persons[{personIndex}].IdVariations[{idVarIndex}]"))
                    {
                        var variation = form[$"Persons[{personIndex}].IdVariations[{idVarIndex}]"].ToString();
                        if (!string.IsNullOrWhiteSpace(variation))
                            content.Add(new StringContent(variation),
                                $"Persons[{personIndex}].IdVariations[{idVarIndex}]");
                        idVarIndex++;
                    }

                    // Variaciones de teléfono
                    var phoneVarIndex = 0;
                    while (form.ContainsKey($"Persons[{personIndex}].PhoneVariations[{phoneVarIndex}]"))
                    {
                        var variation = form[$"Persons[{personIndex}].PhoneVariations[{phoneVarIndex}]"].ToString();
                        if (!string.IsNullOrWhiteSpace(variation))
                            content.Add(new StringContent(variation),
                                $"Persons[{personIndex}].PhoneVariations[{phoneVarIndex}]");
                        phoneVarIndex++;
                    }

                    personIndex++;
                }

                // Datos generales del documento
                content.Add(
                    new StringContent(form["GeneralData.CaseNumber"].ToString() ?? ""),
                    "GeneralData.CaseNumber");
                content.Add(
                    new StringContent(form["GeneralData.OfficeNumber"].ToString() ?? ""),
                    "GeneralData.OfficeNumber");

                if (personIndex == 0)
                    return BadRequest("Debe proporcionar al menos una persona.");

                var response = await client.PostAsync(
                    $"{apiUrl}/api/documents/upload", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, errorBody);
                }

                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                var contentType = file.ContentType ?? "application/octet-stream";

                return File(fileBytes, contentType, $"ANONYMIZED_{file.FileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Upload.Process");
                return StatusCode(500, ex.Message);
            }
        }
    }
}