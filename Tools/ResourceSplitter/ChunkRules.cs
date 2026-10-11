using System.Text;
using System.Text.RegularExpressions;

static class ChunkRules
{
    public const string Default = "# Only matching files larger than 50 MiB are chunked.\n# One pattern per line. ! excludes; the last matching rule wins.\n*\n";
    public const string Unity = "# Unity assets\nAssets/**\n!Assets/Small/**\n";
    public const string Unreal = "# Unreal assets\nContent/**\n";
    public static string PathFor(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(fullRoot, ".gitchunk", relative));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar + ".gitchunk" + Path.DirectorySeparatorChar, FileSystemPaths.Comparison)) throw new Exception("Invalid settings path.");
        for (var current = path; current != null && current.Length >= fullRoot.Length; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new Exception("Settings cannot use symbolic links/junctions.");
        return path;
    }
    public static string EnglishTemplate(string text)
    {
        return text.Replace("\r\n", "\n") switch {
            "# 50 MiB 초과 파일만 분할합니다.\n# 한 줄에 하나씩 지정합니다. !는 제외, 마지막 일치 규칙이 우선합니다.\n*\n" => Default,
            "# Unity 에셋\nAssets/**\n!Assets/Small/**\n" => Unity,
            "# Unreal 에셋\nContent/**\n" => Unreal,
            _ => text
        };
    }
    public static string Load(string root) { var path = PathFor(root, "rules.txt"); return File.Exists(path) ? EnglishTemplate(File.ReadAllText(path)) : Default; }
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
        if (relative.Split('/').Any(x => x is ".git" or ".gitchunk" or ".resourcesplitter") || relative.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("Tools/ResourceSplitter/", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("GitChunk.", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("ResourceSplitter.", StringComparison.OrdinalIgnoreCase) || relative is ".gitattributes" or ".gitignore") return false;
        bool include = false;
        foreach (var rule in Lines(Load(root)))
            if (Regex.IsMatch(relative, Expression(rule.Pattern), OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase : RegexOptions.None)) include = rule.Include;
        return include;
    }
    public static void Save(string root, string text, string relative = "rules.txt")
    {
        Validate(text);
        var path = PathFor(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temp, text.Replace("\r\n", "\n"), new UTF8Encoding(false)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static void EnsureDefaults(string root)
    {
        foreach (var (name, text) in new[] { ("rules.txt", Default) }) {
            var path = PathFor(root, name);
            if (!File.Exists(path)) Save(root, text, name);
            else { var old = File.ReadAllText(path); var translated = EnglishTemplate(old); if (old != translated) Save(root, translated, name); }
        }
    }
}
