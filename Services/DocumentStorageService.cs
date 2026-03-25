using DocsValidator.Data;
using DocsValidator.Models;
using Microsoft.EntityFrameworkCore;

namespace DocsValidator.Services;

public interface IDocumentStorageService
{
    Task<Document> UploadDocumentAsync(Stream fileStream, string fileName, Guid uploadedById, bool hasDigitalSignature, string? signatureOwner);
    Task<(byte[] Content, string FileName)> DownloadDocumentAsync(Guid documentId);
    Task<Document?> GetDocumentAsync(Guid documentId);
    Task<List<Document>> GetUserDocumentsAsync(Guid userId);
    Task UpdateDocumentValidationAsync(Guid documentId, bool isClean, string scanResult);
}

public class DocumentStorageService : IDocumentStorageService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileValidationService _fileValidationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentStorageService> _logger;

    public DocumentStorageService(
        ApplicationDbContext context,
        IFileValidationService fileValidationService,
        IConfiguration configuration,
        ILogger<DocumentStorageService> logger)
    {
        _context = context;
        _fileValidationService = fileValidationService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Document> UploadDocumentAsync(
        Stream fileStream,
        string fileName,
        Guid uploadedById,
        bool hasDigitalSignature,
        string? signatureOwner)
    {
        if (!_fileValidationService.IsValidPdfExtension(fileName))
            throw new InvalidOperationException("Only PDF files are allowed");

        var storagePath = _configuration["FileStorage:Path"]
            ?? throw new InvalidOperationException("FileStorage:Path not configured");

        var uploadDir = Path.Combine(storagePath, "uploads");
        Directory.CreateDirectory(uploadDir);

        var secureFileName = _fileValidationService.GenerateSecureFileName(fileName);
        var filePath = Path.Combine(uploadDir, secureFileName);

        // Buffer the stream once; reuse the bytes for hashing and persisting
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        var fileContent = memoryStream.ToArray();

        await File.WriteAllBytesAsync(filePath, fileContent);

        var document = new Document
        {
            UploadedById = uploadedById,
            OriginalFileName = fileName,
            StoredFileName = secureFileName,
            FilePath = filePath,
            FileSize = fileContent.Length,
            FileHash = _fileValidationService.CalculateFileHash(fileContent),
            HasDigitalSignature = hasDigitalSignature,
            SignatureOwner = signatureOwner,
            IsCleanAccordingToClamAV = false // Updated after scanning
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Document {DocumentId} uploaded: {OriginalFileName} -> {StoredFileName}",
            document.Id, fileName, secureFileName);

        return document;
    }

    public async Task<(byte[] Content, string FileName)> DownloadDocumentAsync(Guid documentId)
    {
        var document = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
        if (document == null)
            throw new InvalidOperationException("Document not found");

        if (!File.Exists(document.FilePath))
            throw new InvalidOperationException("File not found on disk");

        var fileContent = await File.ReadAllBytesAsync(document.FilePath);
        return (fileContent, document.OriginalFileName);
    }

    public async Task<Document?> GetDocumentAsync(Guid documentId)
    {
        return await _context.Documents
            .AsNoTracking()
            .Include(d => d.UploadedBy)
            .FirstOrDefaultAsync(d => d.Id == documentId);
    }

    public async Task<List<Document>> GetUserDocumentsAsync(Guid userId)
    {
        return await _context.Documents
            .AsNoTracking()
            .Where(d => d.UploadedById == userId)
            .Include(d => d.UploadedBy)
            .ToListAsync();
    }

    public async Task UpdateDocumentValidationAsync(Guid documentId, bool isClean, string scanResult)
    {
        var document = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
        if (document == null)
            throw new InvalidOperationException("Document not found");

        document.IsCleanAccordingToClamAV = isClean;
        document.ClamAVScanResult = scanResult;
        document.ClamAVScanDate = DateTime.UtcNow;

        // EF tracks the entity fetched above; no need to call Update() explicitly
        await _context.SaveChangesAsync();

        _logger.LogInformation("Document {DocumentId} validation updated: Clean={IsClean}", documentId, isClean);
    }
}
