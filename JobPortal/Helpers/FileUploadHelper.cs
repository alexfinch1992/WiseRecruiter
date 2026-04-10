namespace JobPortal.Helpers
{
    public class FileUploadResult
    {
        public bool Success { get; set; }
        public string? StoredFileName { get; set; }
        public string? OriginalFileName { get; set; }
        public string? ContentType { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime? UploadedAt { get; set; }
    }
}
