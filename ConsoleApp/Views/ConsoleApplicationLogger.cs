namespace CriticalCare.ConsoleApp;

public sealed class ConsoleApplicationLogger : IApplicationLogger
{
    public void Error(string message, Exception error)
    {
        Console.Error.WriteLine(message + ": " + error.Message);
    }
}
