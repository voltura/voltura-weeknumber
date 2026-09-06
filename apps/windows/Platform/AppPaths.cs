namespace VolturaWeekNumber.Platform;

public sealed record AppPaths(string Data, bool Portable, bool Isolated)
{
    public static AppPaths Resolve(string[] args)
    {
        var testIndex = Array.IndexOf(args, "--isolated-test-mode");
        if (testIndex >= 0)
        {
            if (testIndex + 1 >= args.Length) throw new ArgumentException("An isolated data directory is required.");
            return new(Path.GetFullPath(args[testIndex + 1]), false, true);
        }
        var portable = File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.marker"));
        return new(portable ? Path.Combine(AppContext.BaseDirectory, "Data") :
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Voltura", "WeekNumber"), portable, false);
    }
}
