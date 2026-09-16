namespace EquipmentManagementBackend.Models.Enums;

public enum BackupRequestStatus
{
    requested,
    approved,
    allocated,
    in_transit,
    delivered,
    received,
    returned,
    cancelled,
    escalated,
    reserved,
    unreserved,
    in_use
}
