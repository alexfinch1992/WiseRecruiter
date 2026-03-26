using JobPortal.Helpers;
using JobPortal.Services.Interfaces;

namespace JobPortal.Services.Implementations
{
    /// <summary>
    /// Implementation of IFileUploadService using local filesystem.
    /// Wraps FileUploadHelper to provide interface-based abstraction.
    /// Can be replaced with S3FileUploadService or AzureBlobUploadService without controller changes.
    /// </summary>
    public class LocalFileUploadService : IFileUploadService
    {
        private readonly string _webRootPath;

        public LocalFileUploadService(IWebHostEnvironment webHostEnvironment)
        {
            _webRootPath = webHostEnvironment?.WebRootPath
                ?? throw new ArgumentNullException(nameof(webHostEnvironment));
        }

        public (bool isValid, string? errorMessage) ValidateResume(IFormFile? file)
        {
            return FileUploadHelper.ValidateResume(file);
        }

        public (bool isValid, string? errorMessage) ValidateDocument(IFormFile? file)
        {
            return FileUploadHelper.ValidateDocument(file);
        }

        public async Task<(bool success, string? fileIdentifier, string? errorMessage)> UploadResumeAsync(IFormFile file)
        {
            return await FileUploadHelper.SaveResumeAsync(file, _webRootPath);
        }

        public async Task<(bool success, string? fileIdentifier, string? errorMessage)> UploadDocumentAsync(IFormFile file)
        {
            return await FileUploadHelper.SaveDocumentAsync(file, _webRootPath);
        }

        public async Task<bool> DeleteResumeAsync(string? fileIdentifier)
        {
            return await Task.FromResult(FileUploadHelper.DeleteResume(fileIdentifier, _webRootPath));
        }

        public async Task<bool> DeleteDocumentAsync(string? fileIdentifier)
        {
            return await Task.FromResult(FileUploadHelper.DeleteDocument(fileIdentifier, _webRootPath));
        }
    }
}
