namespace JobPortal.Models
{
    public class Alert
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public string Type { get; set; }

        public string Message { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? RelatedEntityId { get; set; }
        public string? RelatedEntityType { get; set; }

        public string? LinkUrl { get; set; }
    }
}
