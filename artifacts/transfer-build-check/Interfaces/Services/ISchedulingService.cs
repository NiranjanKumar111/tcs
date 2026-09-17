namespace EquipmentManagementBackend.Application;

public interface ISchedulingService
{
    Task<int> GenerateAsync(Session actor);
    Task RunAsync(CancellationToken cancellation);
}
