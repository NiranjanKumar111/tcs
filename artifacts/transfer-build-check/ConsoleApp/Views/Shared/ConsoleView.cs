namespace CriticalCare.ConsoleApp.Views.Shared;

public static class ConsoleView
{
    public static void ShowMessage(string message)
    {
        Console.WriteLine(message);
    }

    public static string ReadMenuChoice(
        string name,
        string role,
        string title,
        IEnumerable<(string Key, string Label)> options)
    {
        Console.WriteLine($"\n{name} [{role}] - {title}");
        foreach (var option in options)
        {
            Console.WriteLine($"{option.Key} {option.Label}");
        }

        Console.WriteLine("0 Back / logout");
        return ConsoleInput.Read("Choice");
    }
}
