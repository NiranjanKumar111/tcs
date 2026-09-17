namespace EquipmentManagementBackend.Application;

public sealed class Session
{
    public long UserId { get; }

    internal Session(long userId) => UserId = userId;
}
