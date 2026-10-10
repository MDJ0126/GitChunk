using System.Security.Cryptography;
using System.Text.Json;

record Part(string Path, long Size, string Sha256);
record Asset(string Path, long Size, string Sha256, List<Part> Parts);
record Manifest(int Version, long ChunkBytes, List<Asset> Files);
record Settings(List<string> IncludePaths, List<string> ExcludeDirectories, List<string> ExcludeFilePatterns)
{
    public static Settings Default => new(new() { "." },
        new() { "Library", "Temp", "Obj", "Logs", "UserSettings", "DerivedDataCache", "Intermediate", "Saved", "Binaries", "node_modules", ".vs", ".idea", ".venv", "venv", "bin", "obj" },
        new() { "*.restore-*" });
}

class Program
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    static string Hash(string path) { using var s = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)); }
    static string Inside(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new Exception("Invalid relative path.");
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Path escapes project: " + relative);
        // Never traverse symbolic links or junctions when reading/writing resources.
        var current = full;
        while (current.Length >= root.Length)
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new Exception("Symbolic link/junction is not supported: " + current);
            var parent = Path.GetDirectoryName(current);
            if (parent == null || parent == current) break;
            current = parent;
        }
        return full;
    }
    static bool Reserved(string relative)
    {
        var segments = relative.Replace('\\', '/').Split('/');
        return segments.Any(s => new[] { ".git", ".svn", ".hg" }.Contains(s.ToLowerInvariant())) ||
            segments.Any(s => s.Equals("ResourceParts", StringComparison.OrdinalIgnoreCase) || s.StartsWith("ResourceParts.staging-", StringComparison.OrdinalIgnoreCase) || s.StartsWith("ResourceParts.previous-", StringComparison.OrdinalIgnoreCase)) ||
            segments[^1].StartsWith("ResourceSplitter.", StringComparison.OrdinalIgnoreCase) ||
            relative.Replace('\\', '/').StartsWith("Tools/ResourceSplitter/", StringComparison.OrdinalIgnoreCase);
    }
    static Settings LoadSettings(string root)
    {
        var path = Inside(root, "ResourceSplitter.settings.json");
        if (!File.Exists(path)) File.WriteAllText(path, JsonSerializer.Serialize(Settings.Default, JsonOptions));
        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? throw new Exception("Invalid settings.");
        if (settings.IncludePaths == null || settings.IncludePaths.Count == 0 || settings.ExcludeDirectories == null || settings.ExcludeFilePatterns == null)
            throw new Exception("Settings lists must be provided; IncludePaths cannot be empty.");
        return settings;
    }
    static bool Excluded(string root, string path, Settings settings)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (Reserved(relative) || relative.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || relative is ".gitattributes" or ".gitignore" || System.Text.RegularExpressions.Regex.IsMatch(relative, @"\.part\d{6,}$")) return true;
        if (relative.Split('/').Any(segment => settings.ExcludeDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase))) return true;
        return settings.ExcludeFilePatterns.Any(pattern => System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, Path.GetFileName(path), ignoreCase: true));
    }
    static IEnumerable<string> Walk(string root, string folder, Settings settings)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(folder))
        {
            if (Excluded(root, path, settings)) continue;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new Exception("Symbolic link/junction: " + path);
            if (Directory.Exists(path)) { foreach (var file in Walk(root, path, settings)) yield return file; }
            else yield return path;
        }
    }
    static IEnumerable<string> Resources(string root, Settings settings)
    {
        foreach (var included in settings.IncludePaths)
        {
            var path = included == "." ? root : Inside(root, included);
            if (included != "." && Excluded(root, path, settings)) continue;
            if (Directory.Exists(path)) foreach (var file in Walk(root, path, settings)) yield return file;
            else if (File.Exists(path)) yield return path;
            else throw new Exception("Included path not found: " + included);
        }
    }
    static void Split(string root, long chunk)
    {
        var manifestPath = Inside(root, "ResourceSplitter.manifest.json");
        if (File.Exists(manifestPath)) throw new Exception("Inline parts already exist; restore them before splitting again.");
        var files = Resources(root, LoadSettings(root)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        if (files.Count == 0) throw new Exception("No resources found.");
        var staging = root;
        var createdParts = new List<string>();
        bool published = false;
        try
        {
            var assets = new List<Asset>();
            var buffer = new byte[1024 * 1024];
            foreach (var file in files)
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                var parts = new List<Part>();
                using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                var size = input.Length;
                // Pointer files do not contain the actual asset data.
                if (size < 1024)
                {
                    using var reader = new StreamReader(input, leaveOpen: true);
                    var head = reader.ReadToEnd();
                    input.Position = 0;
                    if (head.StartsWith("version https://git-lfs.github.com/spec/v1")) throw new Exception("LFS pointer found; download the real asset first: " + relative);
                }
                using var fullHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                int index = 0;
                do
                {
                    var partRel = relative + ".part" + (++index).ToString("D6");
                    var destination = Inside(staging, partRel);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    using var partHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    long written = 0;
                    using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        createdParts.Add(destination);
                        while (written < chunk)
                        {
                            var n = input.Read(buffer, 0, (int)Math.Min(buffer.Length, chunk - written));
                            if (n == 0) break;
                            stream.Write(buffer, 0, n);
                            fullHash.AppendData(buffer, 0, n); partHash.AppendData(buffer, 0, n);
                            written += n;
                        }
                    }
                    parts.Add(new Part(partRel, written, Convert.ToHexString(partHash.GetHashAndReset())));
                } while (input.Position < size);
                if (parts.Sum(p => p.Size) != size) throw new Exception("Resource changed while reading: " + relative);
                assets.Add(new Asset(relative, size, Convert.ToHexString(fullHash.GetHashAndReset()), parts));
                Console.WriteLine($"[{assets.Count}/{files.Count}] {relative} ({parts.Count} parts)");
            }
            // Verify all saved parts and unchanged originals before publishing or deleting.
            foreach (var asset in assets)
            {
                using var restoredHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                foreach (var part in asset.Parts)
                {
                    var path = Inside(staging, part.Path);
                    if (new FileInfo(path).Length != part.Size || Hash(path) != part.Sha256)
                        throw new Exception("Saved part verification failed: " + part.Path);
                    using var stream = File.OpenRead(path);
                    int count;
                    while ((count = stream.Read(buffer, 0, buffer.Length)) > 0) restoredHash.AppendData(buffer, 0, count);
                }
                if (Convert.ToHexString(restoredHash.GetHashAndReset()) != asset.Sha256)
                    throw new Exception("Saved asset verification failed: " + asset.Path);
                var original = Inside(root, asset.Path);
                if (new FileInfo(original).Length != asset.Size || Hash(original) != asset.Sha256)
                    throw new Exception("Original changed; refusing to delete: " + asset.Path);
            }
            var temporaryManifest = manifestPath + ".restore-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temporaryManifest, JsonSerializer.Serialize(new Manifest(2, chunk, assets), JsonOptions));
            try { File.Move(temporaryManifest, manifestPath); }
            finally { if (File.Exists(temporaryManifest)) File.Delete(temporaryManifest); }
            published = true;
            foreach (var asset in assets)
            {
                var original = Inside(root, asset.Path);
                if (new FileInfo(original).Length != asset.Size || Hash(original) != asset.Sha256)
                    throw new Exception("Original changed; verified parts retained, deletion stopped: " + asset.Path);
                File.Delete(original);
                Console.WriteLine("Original removed: " + asset.Path);
            }
            Console.WriteLine($"Split complete: {assets.Count} files. Parts verified; originals removed.");

        }
        catch
        {
            if (!published)
            {
                foreach (var part in createdParts) File.Delete(part);
            }
            else Console.Error.WriteLine("Verified inline parts retained; use restore: " + manifestPath);
            throw;
        }
    }
    static void Join(string root)
    {
        var inlineManifest = Inside(root, "ResourceSplitter.manifest.json");
        bool inline = File.Exists(inlineManifest);
        var output = inline ? root : Inside(root, "ResourceParts");
        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(inline ? inlineManifest : Inside(output, "manifest.json"))) ?? throw new Exception("Invalid manifest.");
        if (manifest.Version != (inline ? 2 : 1) || manifest.Files == null || manifest.Files.Count == 0) throw new Exception("Unsupported/empty manifest.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Validate every part and destination before restoring any file.
        foreach (var asset in manifest.Files)
        {
            var target = Inside(root, asset.Path);
            if (!seen.Add(target) || asset.Parts == null || asset.Parts.Count == 0 || asset.Size < 0 || asset.Parts.Sum(p => p.Size) != asset.Size)
                throw new Exception("Invalid asset entry: " + asset.Path);
            if (Reserved(Path.GetRelativePath(root, target))) throw new Exception("Protected destination: " + asset.Path);
            if (File.Exists(target) && (new FileInfo(target).Length != asset.Size || Hash(target) != asset.Sha256))
                throw new Exception("Existing file differs; move it to a backup before restoring: " + asset.Path);
            foreach (var part in asset.Parts)
            {
                var path = Inside(output, part.Path);
                if (!part.Path.StartsWith(asset.Path + ".part", StringComparison.Ordinal)) throw new Exception("Invalid part path: " + part.Path);
                if (part.Size < 0 || !File.Exists(path) || new FileInfo(path).Length != part.Size || Hash(path) != part.Sha256)
                    throw new Exception("Missing/corrupt part: " + part.Path);
            }
        }
        foreach (var asset in manifest.Files)
        {
            var target = Inside(root, asset.Path);
            if (File.Exists(target)) { Console.WriteLine("Verified existing: " + asset.Path); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var temp = target + ".restore-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = File.Create(temp))
                    foreach (var part in asset.Parts) { using var input = File.OpenRead(Inside(output, part.Path)); input.CopyTo(stream); }
                if (new FileInfo(temp).Length != asset.Size || Hash(temp) != asset.Sha256) throw new Exception("Restored hash mismatch: " + asset.Path);
                File.Move(temp, target);
                Console.WriteLine("Restored: " + asset.Path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        if (inline)
        {
            foreach (var asset in manifest.Files)
            {
                var target = Inside(root, asset.Path);
                if (new FileInfo(target).Length != asset.Size || Hash(target) != asset.Sha256)
                    throw new Exception("Restored file changed; parts retained: " + asset.Path);
            }
            foreach (var asset in manifest.Files)
                foreach (var part in asset.Parts) File.Delete(Inside(root, part.Path));
            File.Delete(inlineManifest);
        }
        Console.WriteLine("Restore complete. All hashes verified.");
    }
    static int Main(string[] args)
    {
        bool interactive = args.Length == 0;
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args.Length > 1 ? args[1] : Directory.GetCurrentDirectory()));
            string? mode;
            if (interactive)
            {
                Console.WriteLine("Resource Splitter / 범용 프로젝트 파일 분할 도구");
                Console.WriteLine($"현재 작업 폴더: {root}");
                Console.WriteLine("Enter를 누르면 위 폴더를 대상으로 실행합니다.");
                Console.WriteLine("터미널에서는 명령을 실행한 폴더가 기준입니다.");
                Console.WriteLine("다른 폴더를 사용하려면 프로젝트 경로를 입력하세요:");
                var selectedRoot = Console.ReadLine()?.Trim().Trim('"');
                if (!string.IsNullOrEmpty(selectedRoot)) root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selectedRoot));
                Console.WriteLine($"대상: {root}\n1. 분할 (Split): 원본 옆에 조각 저장, 검증 후 원본 삭제\n2. 합치기 (Restore): 원본 파일 복원\n0. 종료\n기본: 전체 프로젝트, 캐시 제외 / .meta 유지 / 분할 후 원본 삭제 / 복원 전 에디터 실행 금지 / 백업하고 편집기를 닫고 실행해 주세요.");
                mode = Console.ReadLine();
                if (mode == "0") return 0;
            }
            else
            {
                mode = args[0];
                Console.WriteLine($"대상: {root}");
                if (args.Length < 2) Console.WriteLine("경로를 지정하지 않아 현재 작업 폴더를 사용합니다.");
            }
            if (!Directory.Exists(root)) throw new Exception("Project folder does not exist: " + root);
            long chunk = args.Length > 2 ? long.Parse(args[2]) : 49L * 1024 * 1024;
            if (chunk < 1 || chunk > 49L * 1024 * 1024) throw new Exception("Chunk size must be 1..51380224 bytes (49 MiB).");
            if (mode is "1" or "split") Split(root, chunk);
            else if (mode is "2" or "join") Join(root);
            else throw new Exception("Choose 1/split or 2/join.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("ERROR: " + e.Message); return 1; }
        finally { if (interactive) { Console.WriteLine("Enter 키를 누르면 종료합니다."); Console.ReadLine(); } }
    }
}
