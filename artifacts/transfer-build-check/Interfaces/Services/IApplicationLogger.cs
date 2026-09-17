namespace EquipmentManagementBackend.Application;

public interface IApplicationLogger
{
    void Error(string message, Exception error);
}
