namespace FJob.Observability;

public static class ApplicationPathResolver
{
    public static string ResolveContentRoot(params string[] developmentRelativeSegments)
    {
        var baseDirectory = AppContext.BaseDirectory;

        if (File.Exists(Path.Combine(baseDirectory, "appsettings.json")) ||
            Directory.Exists(Path.Combine(baseDirectory, "wwwroot")))
        {
            return baseDirectory;
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, Path.Combine(developmentRelativeSegments)));
    }
}
