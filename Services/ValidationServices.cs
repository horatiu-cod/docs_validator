using System.Security.Cryptography;
using System.Text;

namespace DocsValidator.Services;

public interface IFileValidationService
{
    bool IsValidPdfExtension(string fileName);
    string GenerateSecureFileName(string originalFileName);
    string CalculateFileHash(byte[] fileContent);
}

public class FileValidationService : IFileValidationService
{
    private const string AllowedExtension = ".pdf";
    private const int MaxFileNameLength = 255;

    public bool IsValidPdfExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension == AllowedExtension;
    }

    public string GenerateSecureFileName(string originalFileName)
    {
        // Generate a secure filename using GUID and original extension
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (extension != AllowedExtension)
            throw new InvalidOperationException("Invalid file extension. Only PDF files are allowed.");

        var secureFileName = $"{Guid.NewGuid():N}{extension}";
        return secureFileName;
    }

    public string CalculateFileHash(byte[] fileContent)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(fileContent);
            return Convert.ToBase64String(hashBytes);
        }
    }
}

public interface IDigitalSignatureValidationService
{
    Task<(bool IsValid, string? SignatureOwner)> ValidateDigitalSignatureAsync(byte[] fileContent, string expectedOwner);
}

public class DigitalSignatureValidationService : IDigitalSignatureValidationService
{
    public async Task<(bool IsValid, string? SignatureOwner)> ValidateDigitalSignatureAsync(byte[] fileContent, string expectedOwner)
    {
        // This would integrate with actual PDF signature validation
        // For now, we'll return a placeholder implementation
        await Task.CompletedTask;

        // In a real implementation, you would:
        // 1. Parse the PDF for signature dictionary
        // 2. Extract the certificate
        // 3. Validate the signature cryptographically
        // 4. Extract the certificate owner name

        return (true, expectedOwner);
    }
}

public interface IClamAVService
{
    Task<(bool IsClean, string? Details)> ScanFileAsync(byte[] fileContent, string fileName);
}

public class ClamAVService : IClamAVService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClamAVService> _logger;

    public ClamAVService(IConfiguration configuration, HttpClient httpClient, ILogger<ClamAVService> logger)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(bool IsClean, string? Details)> ScanFileAsync(byte[] fileContent, string fileName)
    {
        try
        {
            var clamAvUrl = _configuration["ClamAV:Url"];
            if (string.IsNullOrEmpty(clamAvUrl))
            {
                _logger.LogWarning("ClamAV URL not configured. Skipping scan.");
                return (true, "ClamAV not configured");
            }

            using (var content = new MultipartFormDataContent())
            {
                content.Add(new ByteArrayContent(fileContent), "file", fileName);

                var response = await _httpClient.PostAsync($"{clamAvUrl}/scan", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                // Parse ClamAV response (CLEAN or INFECTED message)
                var isClean = responseContent.Contains("CLEAN");
                return (isClean, responseContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning file with ClamAV");
            // Fail secure - mark as potentially unclean if scanning fails
            return (false, $"Scan error: {ex.Message}");
        }
    }
}
