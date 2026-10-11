using System.Text;
using System.Text.RegularExpressions;

static class ChunkIgnore
{
    public const string Default = "# Files matching these patterns are stored whole, without chunking.\n# One pattern per line. Example: Build/**\n";
    public static string IgnorePath(string root)
    {
        var path = Path.Combine(Path.GetFullPath(root), ".chunkignore");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new Exception("Settings cannot use symbolic links/junctions.");
        return path;
    }
    public static string PathFor(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(fullRoot, ".gitchunk", relative));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar + ".gitchunk" + Path.DirectorySeparatorChar, FileSystemPaths.Comparison)) throw new Exception("Invalid settings path.");
        for (var current = path; current != null && current.Length >= fullRoot.Length; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new Exception("Settings cannot use symbolic links/junctions.");
        return path;
    }
    static string ConvertLegacy(string text)
    {
        var rules = Lines(text).ToList();
        var result = new StringBuilder(Default);
        // Legacy rules started excluded; ignore lists start included.
        if (rules.Count > 0 && rules[0].Include && rules[0].Pattern == "*") rules.RemoveAt(0);
        else result.Append("**\n");
        foreach (var rule in rules) result.Append(rule.Include ? "!" : "").Append(rule.Pattern).Append('\n');
        return result.ToString();
    }
    public static string Load(string root)
    {
        var path = IgnorePath(root);
        if (File.Exists(path)) return File.ReadAllText(path);
        var legacy = PathFor(root, "rules.txt");
        return File.Exists(legacy) ? ConvertLegacy(File.ReadAllText(legacy)) : Default;
    }
    static IEnumerable<(bool Include, string Pattern)> Lines(string text)
    {
        foreach (var raw in text.Replace("\r", "").Split('\n')) {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var include = !line.StartsWith('!');
            var pattern = include ? line : line[1..];
            if (pattern.Length == 0 || pattern.Contains('\\') || pattern.Contains('\0') || pattern.Split('/').Contains("..")) throw new Exception("Invalid rule: " + line + ". Use / for paths.");
            yield return (include, pattern);
        }
    }
    public static void Validate(string text) { foreach (var rule in Lines(text)) _ = Expression(rule.Pattern); }
    static string Expression(string pattern)
    {
        var anchored = pattern.StartsWith('/'); pattern = pattern.TrimStart('/');
        if (pattern.EndsWith('/')) pattern += "**";
        var result = new StringBuilder(anchored || pattern.Contains('/') ? "^" : "(?:^|/)");
        for (int i = 0; i < pattern.Length; i++) {
            if (pattern[i] == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*') {
                i++;
                if (i + 1 < pattern.Length && pattern[i + 1] == '/') { result.Append("(?:.*/)?"); i++; }
                else result.Append(".*");
            } else if (pattern[i] == '*') result.Append("[^/]*");
            else if (pattern[i] == '?') result.Append("[^/]");
            else result.Append(Regex.Escape(pattern[i].ToString()));
        }
        return result.Append("(?:/.*)?$").ToString();
    }
    public static bool Matches(string root, string relative)
    {
        relative = relative.Replace('\\', '/');
        if (relative.Split('/').Any(x => x is ".git" or ".gitchunk" or ".resourcesplitter") || relative.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("Tools/ResourceSplitter/", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("GitChunk.", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("ResourceSplitter.", StringComparison.OrdinalIgnoreCase) || relative is ".gitattributes" or ".gitignore" or ".chunkignore") return false;
        bool include = true;
        foreach (var rule in Lines(Load(root)))
            if (Regex.IsMatch(relative, Expression(rule.Pattern), OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase : RegexOptions.None)) include = !rule.Include;
        return include;
    }
    public static void Save(string root, string text)
    {
        Validate(text);
        var path = IgnorePath(root); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temp, text.Replace("\r\n", "\n"), new UTF8Encoding(false)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static void EnsureDefaults(string root)
    {
        if (!File.Exists(IgnorePath(root))) Save(root, Load(root));
        var legacy = PathFor(root, "rules.txt");
        if (File.Exists(legacy)) {
            var git = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
            git.ArgumentList.Add("rev-parse"); git.ArgumentList.Add("--absolute-git-dir");
            using var process = System.Diagnostics.Process.Start(git)!;
            var metadata = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new Exception("Cannot locate Git metadata for legacy settings backup.");
            var backup = Path.Combine(metadata, "gitchunk", "rules-backup-" + Guid.NewGuid().ToString("N") + ".txt");
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            File.Move(legacy, backup);
        }
    }
}
