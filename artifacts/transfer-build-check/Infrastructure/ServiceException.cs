namespace EquipmentManagementBackend.Services;

public class ServiceException : Exception
{
    public ServiceException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
