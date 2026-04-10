using JobPortal.Helpers;

namespace JobPortal.Services.Interfaces
{
    public interface IFileUploadService
    {
        (bool isValid, string? errorMessage) ValidateResume(IFormFile? file);

        (bool isValid, string? errorMessage) ValidateDocument(IFormFile? file);

        Task<FileUploadResult> UploadResumeAsync(IFormFile file);

        Task<FileUploadResult> UploadDocumentAsync(IFormFile file);

        Task<bool> DeleteResumeAsync(string? storedFileName);

        Task<bool> DeleteDocumentAsync(string? storedFileName);

        /// <summary>
        /// Resolves the physical path for a stored resume file, or null if not found.
        /// </summary>
        string? GetResumePhysicalPath(string? storedFileName);

        /// <summary>
        /// Resolves the physical path for a stored document file, or null if not found.
        /// </summary>
        string? GetDocumentPhysicalPath(string? storedFileName);
    }
}
