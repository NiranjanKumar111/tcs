namespace CriticalCare.ConsoleApp.Views.Shared;

public sealed class ConsoleApplicationLogger : IApplicationLogger
{
    public void Error(string message, Exception error)
    {
        Console.Error.WriteLine($"\n[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] ERROR: {message}\n  Details: {error.Message}");
    }
}
