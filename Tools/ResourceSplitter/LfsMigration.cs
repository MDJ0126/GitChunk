using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

static partial class GitIntegration
{
    record LfsAsset(string Path, string Oid, long Size, byte[] Pointer);
    static List<LfsAsset> LfsAssets()
    {
        var assets = new List<LfsAsset>();
        foreach (var entry in Text("ls-files", "--stage", "-z").Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = entry.IndexOf('\t'); var fields = entry[..tab].Split(' ');
            if (fields[2] != "0") throw new Exception("Resolve merge conflicts before installation.");
            if (fields[0] != "100644" && fields[0] != "100755") continue;
            if (long.Parse(Text("cat-file", "-s", fields[1])) > 1024) continue;
            var bytes = Git(null, "cat-file", "blob", fields[1]);
            var content = Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n");
            if (!content.StartsWith("version https://git-lfs.github.com/spec/v1\n")) continue;
            var match = Regex.Match(content, @"\Aversion https://git-lfs.github.com/spec/v1\noid sha256:([0-9a-f]{64})\nsize ([0-9]+)\n?\z");
            if (!match.Success || !long.TryParse(match.Groups[2].Value, out var size)) throw new Exception("Unsupported LFS pointer: " + entry[(tab + 1)..]);
            assets.Add(new(entry[(tab + 1)..], match.Groups[1].Value, size, bytes));
        }
        return assets;
    }
    static bool LfsOriginal(string path, LfsAsset asset)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != asset.Size) return false;
        using var input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input)).Equals(asset.Oid, StringComparison.OrdinalIgnoreCase);
    }
    static bool StandardLfsHook(string text, string name)
    {
        var expected = "#!/bin/sh\ncommand -v git-lfs >/dev/null 2>&1 || { printf >&2 \"\\nThis repository is configured for Git LFS but 'git-lfs' was not found on your path. If you no longer wish to use Git LFS, remove this hook by deleting the '" + name + "' file in the hooks directory (set by 'core.hookspath'; usually '.git/hooks').\\n\"; exit 2; }\ngit lfs " + name + " \"$@\"\n";
        var modern = expected.Replace("printf >&2 \"\\nThis", "printf >&2 \"\\n%s\\n\\n\" \"This").Replace("'.git/hooks').\\n\"", "'.git/hooks').\"");
        return text.Replace("\r\n", "\n") == expected || text.Replace("\r\n", "\n") == modern;
    }
    static void Install(string root)
    {
        Directory.SetCurrentDirectory(Path.GetFullPath(root));
        var actual = Path.GetFullPath(Text("rev-parse", "--show-toplevel"));
        if (!actual.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), FileSystemPaths.Comparison)) throw new Exception("Specify the Git repository root.");
        var assets = LfsAssets();
        if (assets.Count == 0) { InstallCore(root); return; }
        if (Text("status", "--porcelain", "--untracked-files=no").Length != 0)
            throw new Exception("LFS conversion requires a clean tracked working tree and index. Commit or stash your changes first.");
        var hooks = Path.GetFullPath(Text("rev-parse", "--git-path", "hooks"));
        foreach (var name in new[] { "pre-commit", "pre-push", "post-checkout", "post-commit", "post-merge" })
        {
            var path = Path.Combine(hooks, name);
            if (!File.Exists(path)) continue;
            var content = File.ReadAllText(path);
            if (StandardLfsHook(content, name)) continue;
            if (name is "pre-commit" or "pre-push" && !content.Contains("# GitChunk managed hook") && !content.Contains("# ResourceSplitter managed hook"))
                throw new Exception("Custom hook must be integrated manually: " + path);
        }
        // Fetch into LFS's cache without checking out or overwriting working files.
        var sources = new Dictionary<string, string>();
        var missing = assets.Where(a => !LfsOriginal(SafePath(actual, a.Path), a)).ToList();
        if (missing.Count > 0)
        {
            var media = Text("lfs", "env").Split('\n').FirstOrDefault(l => l.StartsWith("LocalMediaDir="))?[14..].TrimEnd('\r')
                ?? throw new Exception("Cannot locate Git LFS cache. Install Git LFS to download originals.");
            string Cache(LfsAsset a) => Path.Combine(media, a.Oid[..2], a.Oid[2..4], a.Oid);
            if (missing.Any(a => !LfsOriginal(Cache(a), a))) { Console.WriteLine("Downloading current LFS originals…"); Text("lfs", "fetch"); }
            foreach (var asset in missing)
            {
                var work = SafePath(actual, asset.Path);
                if (!File.Exists(work) || new FileInfo(work).Length != asset.Pointer.Length || !File.ReadAllBytes(work).AsSpan().SequenceEqual(asset.Pointer)) throw new Exception("LFS original is missing or modified: " + asset.Path);
                if (!LfsOriginal(Cache(asset), asset)) throw new Exception("LFS original download/hash verification failed: " + asset.Path);
                sources[asset.Path] = Cache(asset);
            }
        }
        // Preserve index, settings and hooks; rollback keeps the repository usable.
        var metadata = Path.GetFullPath(Text("rev-parse", "--git-path", "gitchunk-lfs-backup-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(metadata);
        var attributeFiles = Text("ls-files", "-z").Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p == ".gitattributes" || p.EndsWith("/.gitattributes", StringComparison.Ordinal)).Select(p => SafePath(actual, p)).ToList();
        var paths = new[] { Path.Combine(actual, ".gitattributes"), Path.Combine(actual, ".gitignore"), Path.GetFullPath(Text("rev-parse", "--git-path", "index")), Path.GetFullPath(Text("rev-parse", "--git-path", "config")) }
            .Concat(attributeFiles).Distinct()
            .Concat(new[] { "pre-commit", "pre-push", "post-checkout", "post-commit", "post-merge" }.Select(n => Path.Combine(hooks, n))).ToList();
        var backups = paths.Select((p, i) => (Path: p, Backup: Path.Combine(metadata, i.ToString()), Exists: File.Exists(p))).ToList();
        foreach (var item in backups) if (item.Exists) File.Copy(item.Path, item.Backup);
        try
        {
            foreach (var asset in missing) File.Copy(sources[asset.Path], SafePath(actual, asset.Path), true);
            foreach (var name in new[] { "pre-push", "post-checkout", "post-commit", "post-merge" })
            {
                var path = Path.Combine(hooks, name);
                if (File.Exists(path) && StandardLfsHook(File.ReadAllText(path), name)) File.Delete(path);
            }
            InstallCore(root);
            foreach (var path in attributeFiles)
            {
                var lines = File.ReadAllLines(path).Select(line => line.TrimStart().StartsWith('#') ? line :
                    Regex.IsMatch(line, @"(?:^|\s)filter=lfs(?:\s|$)") ? Regex.Replace(Regex.Replace(Regex.Replace(line, @"(?<=\s)filter=lfs(?=\s|$)", "filter=gitchunk"), @"(?<=\s)diff=lfs(?=\s|$)", "-diff"), @"(?<=\s)merge=lfs(?=\s|$)", "-merge") : line);
                File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
            }
            // Explicit overrides also remove LFS diff/merge drivers for converted paths.
            var overrides = assets.Select(a => "\"/" + a.Path.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\" filter=gitchunk -text -diff -merge");
            File.AppendAllText(Path.Combine(actual, ".gitattributes"), "\n# GitChunk converted LFS files\n" + string.Join("\n", overrides) + "\n", new UTF8Encoding(false));
            foreach (var asset in assets)
            {
                var attr = Text("check-attr", "filter", "--", asset.Path);
                if (!attr.EndsWith(": gitchunk")) throw new Exception("A nested or local attribute overrides GitChunk: " + asset.Path + ". Adjust that attribute and retry.");
                if (asset.Size > Threshold && !ChunkRules.Matches(actual, asset.Path)) throw new Exception("LFS file excluded by chunk rules: " + asset.Path + ". Include it and retry.");
                Text("add", "--renormalize", "--", asset.Path);
                var staged = Git(null, "show", ":" + asset.Path);
                if (asset.Size > Threshold)
                {
                    var pointer = Parse(staged) ?? throw new Exception("Conversion did not produce a chunk pointer: " + asset.Path);
                    var verification = Path.Combine(metadata, "verify-original");
                    try {
                        using (var restored = File.Create(verification)) Smudge(new MemoryStream(staged), restored);
                        if (pointer.Size != asset.Size || !LfsOriginal(verification, asset)) throw new Exception("Converted original hash mismatch: " + asset.Path);
                    } finally { if (File.Exists(verification)) File.Delete(verification); }
                }
                else if (!Hash(staged).Equals(asset.Oid, StringComparison.OrdinalIgnoreCase)) throw new Exception("Converted original hash mismatch: " + asset.Path);
            }
            Prepare();
            foreach (var path in attributeFiles) Text("add", "--", Path.GetRelativePath(actual, path));
            Text("add", "--", ".gitattributes", ".gitignore", ".gitchunk/rules.txt", ".gitchunk/presets");
            Console.WriteLine($"Converted and verified {assets.Count} current LFS files. Review git diff --cached, then commit and push. No commit or push was performed. Past commits and server LFS storage remain unchanged. Backup: {metadata}");
        }
        catch
        {
            foreach (var item in backups) { if (item.Exists) File.Copy(item.Backup, item.Path, true); else if (File.Exists(item.Path)) File.Delete(item.Path); }
            foreach (var asset in missing) File.WriteAllBytes(SafePath(actual, asset.Path), asset.Pointer);
            Console.Error.WriteLine("LFS conversion failed; original index, settings, hooks and pointer working files restored. Backup: " + metadata);
            throw;
        }
    }
}
