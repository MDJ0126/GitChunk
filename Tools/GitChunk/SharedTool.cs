static class SharedTool
{
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
