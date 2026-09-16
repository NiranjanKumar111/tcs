using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class TicketDocument
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public TicketDocumentType DocumentType { get; set; }

    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string StoredFileName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string StoragePath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public long UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
