namespace EquipmentManagementBackend.Models.Enums;

public enum TicketStatus { open, in_progress, pending_approval, approved, rejected, completed, overdue, cancelled }
public enum TicketPriority { low, medium, high, critical }
public enum TicketHistoryAction { created, status_changed, assigned, reassigned, approved, rejected, completed, comment_added }
public enum TicketDocumentType { maintenance_report, calibration_certificate, photo, invoice, other }
public enum WardType { ICU, ER, OT, GENERAL, OTHER }
public enum BackupRequestPriority { low, medium, high, critical }
public enum BackupRequestStatus { requested, approved, allocated, in_transit, delivered, received, returned, cancelled, escalated, reserved, unreserved, in_use }
public enum BackupAllocationStatus { reserved, in_use, expired, returned, cancelled }
public enum EquipmentAvailability { available, reserved, in_use, inactive }
public enum AuditAction { create, update, delete, view, login, logout, approve, reject, assign, upload, download }
public enum AuditResult { success, failure }
public enum NotificationType { info, warning, critical, assignment, approval, maintenance_due, backup }
