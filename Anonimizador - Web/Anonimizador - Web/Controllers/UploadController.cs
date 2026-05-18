using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anonimizador___Web.Controllers
{
    [Route("upload")]
    [Authorize]
    public class UploadController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<UploadController> _logger;

        public UploadController(
            IConfiguration configuration,
            ILogger<UploadController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

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
                    return BadRequest("No file received");

                var apiUrl = _configuration["ApiSettings:BaseUrl"];
                var token = User.FindFirst("jwt_token")?.Value ?? _configuration["ApiSettings:Token"];

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

                content.Add(new StringContent(form["UploadedBy"].ToString() ?? ""), "UploadedBy");
                content.Add(new StringContent(form["FullName"].ToString() ?? ""), "FullName");
                content.Add(new StringContent(form["Identification"].ToString() ?? ""), "Identification");
                content.Add(new StringContent(form["Email"].ToString() ?? ""), "Email");
                content.Add(new StringContent(form["PhoneNumber"].ToString() ?? ""), "PhoneNumber");
                content.Add(new StringContent(form["Position"].ToString() ?? ""), "Position");
                content.Add(new StringContent(form["Address"].ToString() ?? ""), "Address");

                _logger.LogInformation("Calling API: {Url}", $"{apiUrl}/api/documents/upload");

                var response = await client.PostAsync(
                    $"{apiUrl}/api/documents/upload", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("API error: {Status} {Body}",
                        response.StatusCode, errorBody);
                    return StatusCode((int)response.StatusCode, errorBody);
                }

                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = $"ANONYMIZED_{file.FileName}";

                return File(fileBytes,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Upload.Process: {Message}", ex.Message);
                return StatusCode(500, ex.Message);
            }
        }
    }
}