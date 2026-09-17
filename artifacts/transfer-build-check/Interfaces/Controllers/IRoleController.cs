namespace EquipmentManagementBackend.Controllers;

public interface IRoleController
{
    string Role { get; }

    Task RunAsync(Session session);
}
