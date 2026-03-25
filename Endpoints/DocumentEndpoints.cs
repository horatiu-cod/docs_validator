using DocsValidator.Models;
using DocsValidator.Services;

namespace DocsValidator.Endpoints;

public static class DocumentEndpoints
{
    private const long MaxFileSizeBytes = 100L * 1024 * 1024; // 100 MB

    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/documents").WithName("Documents");

        group.MapPost("/upload", UploadDocument)
            .WithName("UploadDocument")
            .Accepts<IFormFile>("multipart/form-data")
            .RequireAuthorization();

        group.MapGet("/{documentId}/download", DownloadDocument)
            .WithName("DownloadDocument")
            .RequireAuthorization();

        group.MapGet("/{documentId}", GetDocument)
            .WithName("GetDocument")
            .RequireAuthorization();

        group.MapGet("/", ListUserDocuments)
            .WithName("ListUserDocuments")
            .RequireAuthorization();

        group.MapGet("/{documentId}/validation-status", GetValidationStatus)
            .WithName("GetValidationStatus")
            .RequireAuthorization();
    }

    private static async Task<IResult> UploadDocument(
        HttpContext httpContext,
        IFormCollection form,
        IDocumentStorageService documentStorageService,
        IWorkflowService workflowService,
        IClamAVService clamAVService,
        IDigitalSignatureValidationService signatureService,
        ILoggerFactory loggerFactory)
    {
        var userId = EndpointHelpers.GetUserId(httpContext);
        if (userId is null) return Results.Unauthorized();

        var file = form.Files.Count > 0 ? form.Files[0] : null;
        if (file is null || file.Length == 0)
            return Results.BadRequest("No file provided");

        if (file.Length > MaxFileSizeBytes)
            return Results.BadRequest("File size exceeds 100 MB limit");

        try
        {
            using var stream = file.OpenReadStream();
            var document = await documentStorageService.UploadDocumentAsync(
                stream,
                file.FileName,
                userId.Value,
                hasDigitalSignature: bool.TryParse(form["hasDigitalSignature"], out var sig) && sig,
                signatureOwner: form["signatureOwner"].ToString()
            );

            // Initiate workflow and add the first validation step
            var workflow = await workflowService.InitiateWorkflowAsync(document.Id, userId.Value);
            await workflowService.AddValidationStepAsync(workflow.Id);

            // The file bytes are already on disk; read them once for downstream scanning
            var fileContent = await File.ReadAllBytesAsync(document.FilePath);
            var (isClean, details) = await clamAVService.ScanFileAsync(fileContent, document.StoredFileName);
            await documentStorageService.UpdateDocumentValidationAsync(document.Id, isClean, details ?? "No details");

            if (!isClean)
            {
                await workflowService.RejectWorkflowAsync(workflow.Id, "File failed ClamAV scan");
                return Results.BadRequest(new { error = "File failed security scan", details });
            }

            // Validate digital signature if present
            if (document.HasDigitalSignature && !string.IsNullOrEmpty(document.SignatureOwner))
            {
                var (isValidSignature, _) = await signatureService.ValidateDigitalSignatureAsync(
                    fileContent, document.SignatureOwner);

                if (!isValidSignature)
                {
                    await workflowService.RejectWorkflowAsync(workflow.Id, "Digital signature validation failed");
                    return Results.BadRequest("Digital signature validation failed");
                }
            }

            return Results.Created($"/api/documents/{document.Id}", new
            {
                document.Id,
                document.OriginalFileName,
                document.StoredFileName,
                workflowId = workflow.Id,
                status = workflow.Status.ToString()
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger(nameof(DocumentEndpoints))
                .LogError(ex, "Unexpected error uploading document for user {UserId}", userId);
            return Results.StatusCode(500);
        }
    }

    private static async Task<IResult> DownloadDocument(
        Guid documentId,
        HttpContext httpContext,
        IDocumentStorageService documentStorageService,
        IAuthorizationService authorizationService)
    {
        var userId = EndpointHelpers.GetUserId(httpContext);
        if (userId is null) return Results.Unauthorized();

        if (!await authorizationService.CanAccessDocumentAsync(userId.Value, documentId))
            return Results.Forbid();

        try
        {
            var (fileContent, fileName) = await documentStorageService.DownloadDocumentAsync(documentId);
            return Results.File(fileContent, "application/pdf", fileName);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> GetDocument(
        Guid documentId,
        HttpContext httpContext,
        IDocumentStorageService documentStorageService,
        IAuthorizationService authorizationService)
    {
        var userId = EndpointHelpers.GetUserId(httpContext);
        if (userId is null) return Results.Unauthorized();

        if (!await authorizationService.CanAccessDocumentAsync(userId.Value, documentId))
            return Results.Forbid();

        var document = await documentStorageService.GetDocumentAsync(documentId);
        if (document == null) return Results.NotFound();

        return Results.Ok(new
        {
            document.Id,
            document.OriginalFileName,
            document.StoredFileName,
            document.FileSize,
            document.UploadedAt,
            document.HasDigitalSignature,
            document.SignatureOwner,
            document.IsCleanAccordingToClamAV,
            document.ClamAVScanDate
        });
    }

    private static async Task<IResult> ListUserDocuments(
        HttpContext httpContext,
        IDocumentStorageService documentStorageService)
    {
        var userId = EndpointHelpers.GetUserId(httpContext);
        if (userId is null) return Results.Unauthorized();

        var documents = await documentStorageService.GetUserDocumentsAsync(userId.Value);
        return Results.Ok(documents.Select(d => new
        {
            d.Id,
            d.OriginalFileName,
            d.StoredFileName,
            d.FileSize,
            d.UploadedAt,
            d.IsCleanAccordingToClamAV
        }));
    }

    private static async Task<IResult> GetValidationStatus(
        Guid documentId,
        HttpContext httpContext,
        IDocumentStorageService documentStorageService,
        IAuthorizationService authorizationService)
    {
        var userId = EndpointHelpers.GetUserId(httpContext);
        if (userId is null) return Results.Unauthorized();

        if (!await authorizationService.CanAccessDocumentAsync(userId.Value, documentId))
            return Results.Forbid();

        var document = await documentStorageService.GetDocumentAsync(documentId);
        if (document == null) return Results.NotFound();

        return Results.Ok(new
        {
            document.Id,
            document.IsCleanAccordingToClamAV,
            document.ClamAVScanResult,
            document.ClamAVScanDate,
            document.HasDigitalSignature
        });
    }
}
