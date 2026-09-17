namespace EquipmentManagementBackend.Application;

public static class InputValidation
{
    public static void RequireText(string? value, int maximum, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
        {
            throw new ArgumentException($"{label} is required (maximum {maximum} characters).");
        }
    }
}
