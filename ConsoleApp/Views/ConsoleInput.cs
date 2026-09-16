using EquipmentManagementBackend.DTOs;

namespace CriticalCare.ConsoleApp;

public static class ConsoleInput
{
    public static string Read(string prompt)
    {
        Console.Write(prompt + ": ");
        return Console.ReadLine()?.Trim() ?? throw new EndOfStreamException();
    }

    public static string? Optional(string prompt)
    {
        var value = Read(prompt);
        return value.Length == 0 ? null : value;
    }

    public static long? OptionalId(string prompt)
    {
        var value = Optional(prompt);
        return value == null ? null : long.Parse(value);
    }

    public static long Number(string prompt, long? fallback = null)
    {
        var value = Read(prompt);
        if (value.Length == 0 && fallback != null)
        {
            return fallback.Value;
        }

        if (!long.TryParse(value, out var number) || number < 1 || number > int.MaxValue)
        {
            throw new ArgumentException("Enter a positive whole number.");
        }

        return number;
    }

    public static DateOnly Date(string prompt) => DateOnly.ParseExact(Read(prompt + " YYYY-MM-DD"), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    public static decimal Decimal(string prompt) => decimal.Parse(Read(prompt), System.Globalization.CultureInfo.InvariantCulture);
    public static bool Yes(string prompt)
    {
        var answer = Read(prompt + " y/n").ToLowerInvariant();
        return answer switch
        {
            "y" => true,
            "n" => false,
            _ => throw new ArgumentException("Enter y or n.")};
    }

    public static T Choice<T>(string prompt)
    where T : struct, Enum
    {
        var value = Read(prompt + " [" + string.Join('/', Enum.GetNames<T>()) + "]");
        return Enum.TryParse<T>(value, true, out var result) && Enum.IsDefined(result) ? result : throw new ArgumentException("Choose a listed value.");
    }

    public static string Password(string prompt)
    {
        Console.Write(prompt + ": ");
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? throw new EndOfStreamException();
        }

        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return new string (chars.ToArray());
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                }
            }
            else if (!char.IsControl(key.KeyChar) && chars.Count < 128)
            {
                chars.Add(key.KeyChar);
            }
        }
    }
}
