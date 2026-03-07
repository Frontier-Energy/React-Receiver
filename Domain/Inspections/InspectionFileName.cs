namespace React_Receiver.Domain.Inspections;

public static class InspectionFileName
{
    public static string Sanitize(string? fileName, int index)
    {
        var safeFileName = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(safeFileName) ? $"file_{index}" : safeFileName;
    }
}
