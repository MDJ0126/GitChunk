static class SharedTool
{
    public static string DebugCommand()
    {
        var configured = Environment.GetEnvironmentVariable("GITCHUNK_COMMAND");
        if (!string.IsNullOrWhiteSpace(configured))
            return File.Exists(configured) ? Path.GetFullPath(configured) : throw new Exception("GITCHUNK_COMMAND does not point to an installed command.");
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[] {
            Path.Combine(local, "Programs", "GitChunk", "bin", "git-chunk.exe"),
            Path.Combine(local, "GitChunk", "bin", "git-chunk.exe")
        }.Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Where(path => !string.IsNullOrWhiteSpace(path)).Select(path => Path.Combine(path.Trim('"'), "git-chunk.exe")));
        return candidates.FirstOrDefault(File.Exists) ?? throw new Exception("Install Git Chunk first. The optional debug tool requires the shared git-chunk command.");
    }
    public static string Executable()
    {
        var source = Path.GetFullPath(Environment.ProcessPath ?? throw new Exception("Executable path unavailable."));
        var folder = Path.GetDirectoryName(source)!;
        // The GUI is a debugging/management tool; prefer its packaged CLI.
        foreach (var candidate in OperatingSystem.IsWindows()
            ? new[] { Path.Combine(folder, "bin", "git-chunk.exe"), Path.Combine(folder, "git-chunk.exe") }
            : new[] { Path.Combine(folder, "GitChunk-cli"), Path.Combine(folder, "git-chunk") })
            if (File.Exists(candidate)) return candidate;
        if (RepositoryContext.FromExecutable(source) != null)
            throw new Exception("Install the shared Git Chunk application first, then run git chunk install from that installation.");
        return source;
    }
}
