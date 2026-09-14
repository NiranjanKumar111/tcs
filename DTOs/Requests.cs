using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public record CreateEquipmentRequest(string EquipmentCode, string Name, long EquipmentTypeId, long LocationId, string? SerialNumber, string? Manufacturer, string? ModelNumber, DateOnly? PurchaseDate, long? CreatedBy);
public record UpdateEquipmentRequest(string Name, long EquipmentTypeId, long LocationId, string? SerialNumber, string? Manufacturer, string? ModelNumber, DateOnly? PurchaseDate, bool IsActive, long? UpdatedBy);
public record CreateTicketRequest(long EquipmentId, long CreatedById, string Title, string? Description, TicketPriority Priority, DateOnly DueDate);
public record UpdateTicketStatusRequest(TicketStatus Status, long PerformedBy, string? Remarks, int VersionNumber);
public record AssignEquipmentUserRequest(long UserId);
public record CreateBackupRequestRequest(WardType RequestedWard, long RequestedBy, long EquipmentTypeId,
    string BedNumber, long? RequestedEquipmentId = null, int Quantity = 1,
    BackupRequestPriority Priority = BackupRequestPriority.medium, string Reason = "");
