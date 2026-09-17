namespace CriticalCare.ConsoleApp.Views.Shared;

public static class ConsoleView
{
    public static string Friendly(object value) =>
        System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToString()!.Replace('_', ' '));

    public static void Heading(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 64));
        Console.WriteLine("  " + title);
        Console.WriteLine(new string('=', 64));
    }

    public static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine("  -- " + title + " --");
    }

    public static void Field(string label, object? value)
    {
        var text = value?.ToString();
        Console.WriteLine($"  {label,-20}: {(string.IsNullOrWhiteSpace(text) ? "Not recorded" : text)}");
    }

    public static void ShowMessage(string message)
    {
        Console.WriteLine("\n  " + message);
    }

    public static string ReadMenuChoice(
        string name,
        string role,
        string title,
        IEnumerable<(string Key, string Label)> options)
    {
        Heading(title);
        Console.WriteLine($"  Signed in: {name} | Role: {Friendly(role)}");
        Console.WriteLine();
        foreach (var option in options)
        {
            Console.WriteLine($"  {option.Key} {option.Label}");
        }

        Console.WriteLine("\n  0 Back / logout");
        Console.WriteLine(new string('-', 64));
        return ConsoleInput.Read("Choice");
    }
}
