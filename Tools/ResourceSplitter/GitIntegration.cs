using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

// The original path stores a small pointer in Git; chunk blobs are ordinary Git files.
// Reading chunks by blob ID makes checkout independent of file checkout order.
static class GitIntegration
{
    const long Threshold = 50L * 1024 * 1024;
    const int ChunkSize = 49 * 1024 * 1024;
    const string Prefix = "resourcesplitter-pointer-v1\n";
    const string Store = ".resourcesplitter/objects/";
    static string InstalledToolName => OperatingSystem.IsWindows() ? "ResourceSplitter.exe" : "ResourceSplitter-osx-" + (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64");
    static bool ToolPath(string path) => path is "ResourceSplitter.exe" or ".resourcesplitter/ResourceSplitter.exe" or "ResourceSplitter-osx-arm64" or "ResourceSplitter-osx-x64" or ".resourcesplitter/ResourceSplitter-osx-arm64" or ".resourcesplitter/ResourceSplitter-osx-x64";
    record Chunk(string Oid, int Size, string Sha256);
    record Pointer(long Size, string Sha256, List<Chunk> Chunks);

    static byte[] Git(byte[]? input, params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new Exception("Cannot start Git.");
        var errors = process.StandardError.ReadToEndAsync();
        var write = Task.Run(() => { try { if (input != null) process.StandardInput.BaseStream.Write(input); } finally { process.StandardInput.Close(); } });
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        write.GetAwaiter().GetResult();
        process.WaitForExit();
        var error = errors.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new Exception("git " + args[0] + ": " + error.Trim());
        return output.ToArray();
    }
    static string Text(params string[] args) => Encoding.UTF8.GetString(Git(null, args)).TrimEnd('\r', '\n');
    static bool Oid(string value) => Regex.IsMatch(value, "\\A(?:[0-9a-f]{40}|[0-9a-f]{64})\\z");
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    static Dictionary<string, Pointer> Pointers(IEnumerable<string> ids)
    {
        var result = new Dictionary<string, Pointer>();
        // Batch plumbing avoids starting a Git process for every small project file.
        foreach (var group in ids.Distinct().Chunk(64))
        {
            var data = Git(Encoding.ASCII.GetBytes(string.Join("\n", group) + "\n"), "cat-file", "--batch");
            int position = 0;
            foreach (var id in group)
            {
                var newline = Array.IndexOf(data, (byte)'\n', position);
                if (newline < 0) throw new Exception("Invalid Git batch response.");
                var fields = Encoding.ASCII.GetString(data, position, newline - position).Split(' ');
                if (fields.Length != 3 || fields[1] != "blob" || fields[0] != id) throw new Exception("Missing blob: " + id);
                var length = int.Parse(fields[2]); position = newline + 1;
                if (length < 0 || length > 1024 * 1024 || position + length >= data.Length) throw new Exception("Invalid Git blob size.");
                var pointer = Parse(data.AsSpan(position, length).ToArray());
                if (pointer != null) result[id] = pointer;
                position += length + 1;
            }
        }
        return result;
    }
    static Pointer? Parse(byte[] bytes)
    {
        var prefix = Encoding.UTF8.GetBytes(Prefix);
        if (!bytes.AsSpan().StartsWith(prefix)) return null;
        if (bytes.Length > 1024 * 1024) throw new Exception("Pointer exceeds size limit.");
        var p = JsonSerializer.Deserialize<Pointer>(bytes.AsSpan(prefix.Length)) ?? throw new Exception("Invalid pointer.");
        if (p.Size <= Threshold || p.Chunks == null || p.Chunks.Count == 0 || p.Chunks.Any(c => !Oid(c.Oid) || c.Size < 1 || c.Size > ChunkSize || !Regex.IsMatch(c.Sha256, "\\A[0-9A-F]{64}\\z"))
            || p.Chunks.Sum(c => (long)c.Size) != p.Size || !Regex.IsMatch(p.Sha256, "\\A[0-9A-F]{64}\\z")) throw new Exception("Invalid pointer entries.");
        return p;
    }
    static void Clean()
    {
        // Spool to a private temporary file so memory use is bounded by one chunk.
        var temp = Path.Combine(Path.GetTempPath(), "ResourceSplitter-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var file = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            Console.OpenStandardInput().CopyTo(file);
            file.Position = 0;
            var output = Console.OpenStandardOutput();
            if (file.Length <= Threshold)
            {
                // Reject malformed reserved pointers instead of silently accepting them.
                if (file.Length <= 1024 * 1024) { var b = new byte[(int)file.Length]; file.ReadExactly(b); Parse(b); output.Write(b); }
                else file.CopyTo(output);
                return;
            }
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var chunks = new List<Chunk>();
            var root = Text("rev-parse", "--show-toplevel");
            var directory = Path.Combine(root, Store);
            if ((Directory.Exists(Path.Combine(root, ".resourcesplitter")) && (File.GetAttributes(Path.Combine(root, ".resourcesplitter")) & FileAttributes.ReparsePoint) != 0)
                || (Directory.Exists(directory) && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)) throw new Exception("Chunk directory cannot be a symbolic link/junction.");
            Directory.CreateDirectory(directory);
            while (file.Position < file.Length)
            {
                var bytes = new byte[(int)Math.Min(ChunkSize, file.Length - file.Position)];
                file.ReadExactly(bytes); hash.AppendData(bytes);
                var oid = Encoding.ASCII.GetString(Git(bytes, "hash-object", "-w", "--stdin")).Trim();
                if (!Oid(oid)) throw new Exception("Invalid Git object ID.");
                // Stash does not run pre-commit. Keep generated chunks reachable
                // locally even when only an uncommitted/stashed pointer uses them.
                Text("update-ref", "refs/resourcesplitter/cache/" + oid, oid);
                chunks.Add(new Chunk(oid, bytes.Length, Hash(bytes)));
                var path = Path.Combine(directory, oid);
                if (!File.Exists(path))
                {
                    var staged = path + ".tmp-" + Guid.NewGuid().ToString("N");
                    try { File.WriteAllBytes(staged, bytes); File.Move(staged, path, false); }
                    finally { if (File.Exists(staged)) File.Delete(staged); }
                }
            }
            output.Write(Encoding.UTF8.GetBytes(Prefix + JsonSerializer.Serialize(new Pointer(file.Length, Convert.ToHexString(hash.GetHashAndReset()), chunks)) + "\n"));
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    static string BlobId(byte[] data, bool sha256)
    {
        var header = Encoding.ASCII.GetBytes("blob " + data.Length + "\0");
        using var hash = IncrementalHash.CreateHash(sha256 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA1);
        hash.AppendData(header); hash.AppendData(data);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
    static string SafePath(string root, string relative)
    {
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, FileSystemPaths.Comparison)) throw new Exception("Path outside project.");
        for (var current = path; current != null && current.Length >= Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).Length; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new Exception("Symbolic link/junction is not supported: " + current);
        }
        return path;
    }
    public static void ManualSplit(string root, IEnumerable<string> files, int chunkSize)
    {
        var storeRoot = StorageRoot(root);
        bool sha256 = false;
        try { sha256 = Text("-C", storeRoot, "rev-parse", "--show-object-format") == "sha256"; } catch { /* Standalone folders do not require Git. */ }
        int count = 0;
        foreach (var original in files.ToList())
        {
            if (new FileInfo(original).Length <= Threshold) continue;
            var path = SafePath(root, Path.GetRelativePath(root, original));
            var chunks = new List<Chunk>();
            using var fullHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long size;
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                size = input.Length;
                while (input.Position < size)
                {
                    var bytes = new byte[(int)Math.Min(chunkSize, size - input.Position)]; input.ReadExactly(bytes);
                    fullHash.AppendData(bytes);
                    var oid = BlobId(bytes, sha256); var hash = Hash(bytes);
                    var partPath = SafePath(storeRoot, Store + oid); Directory.CreateDirectory(Path.GetDirectoryName(partPath)!);
                    if (File.Exists(partPath))
                    {
                        var existing = File.ReadAllBytes(partPath);
                        if (existing.Length != bytes.Length || Hash(existing) != hash) throw new Exception("Corrupt existing chunk: " + oid);
                    }
                    else
                    {
                        var temp = partPath + ".tmp-" + Guid.NewGuid().ToString("N");
                        try { File.WriteAllBytes(temp, bytes); File.Move(temp, partPath); }
                        finally { if (File.Exists(temp)) File.Delete(temp); }
                    }
                    if (Hash(File.ReadAllBytes(partPath)) != hash) throw new Exception("Saved chunk verification failed.");
                    chunks.Add(new Chunk(oid, bytes.Length, hash));
                }
            }
            var pointer = new Pointer(size, Convert.ToHexString(fullHash.GetHashAndReset()), chunks);
            var temporary = path + ".restore-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporary, Encoding.UTF8.GetBytes(Prefix + JsonSerializer.Serialize(pointer) + "\n"));
                using (var verify = File.OpenRead(path))
                    if (verify.Length != size || Convert.ToHexString(SHA256.HashData(verify)) != pointer.Sha256) throw new Exception("Original changed during splitting: " + path);
                File.Move(temporary, path, true); count++;
                Console.WriteLine("Pointer saved: " + Path.GetRelativePath(root, path));
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        Console.WriteLine($"Split complete: {count} files. Metadata unchanged; chunks retained in {Store}.");
    }
    static IEnumerable<string> PointerFiles(string root, string directory)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            if (new[] { ".git", ".svn", ".hg", ".resourcesplitter", "ResourceParts" }.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)) continue;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
            if (Directory.Exists(path)) { foreach (var file in PointerFiles(root, path)) yield return file; }
            else if (new FileInfo(path).Length <= 1024 * 1024 && Parse(File.ReadAllBytes(path)) != null) yield return SafePath(root, Path.GetRelativePath(root, path));
        }
    }
    public static void ManualJoin(string root)
    {
        var storeRoot = StorageRoot(root);
        int count = 0;
        foreach (var path in PointerFiles(root, root).ToList())
        {
            var bytes = File.ReadAllBytes(path); var temp = path + ".restore-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var output = File.Create(temp)) Smudge(new MemoryStream(bytes), output, storeRoot);
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) throw new Exception("Pointer changed during restore: " + path);
                File.Move(temp, path, true); count++;
                Console.WriteLine("Restored: " + Path.GetRelativePath(root, path));
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        Console.WriteLine($"Restore complete: {count} files. Chunks retained.");
    }
    static string StorageRoot(string root)
    {
        try { return Text("-C", root, "rev-parse", "--show-toplevel"); }
        catch { return root; }
    }
    static void Smudge(Stream source, Stream destination, string? root = null)
    {
        var prefix = Encoding.UTF8.GetBytes(Prefix);
        var head = new byte[prefix.Length];
        var count = source.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        if (count != head.Length || !head.AsSpan().SequenceEqual(prefix))
        {
            destination.Write(head.AsSpan(0, count)); source.CopyTo(destination); return;
        }
        using var input = new MemoryStream(); input.Write(head);
        var buffer = new byte[8192]; int read;
        while ((read = source.Read(buffer)) != 0)
        {
            if (input.Length + read > 1024 * 1024) throw new Exception("Pointer exceeds size limit.");
            input.Write(buffer, 0, read);
        }
        var bytes = input.ToArray(); var pointer = Parse(bytes);
        if (pointer == null) { destination.Write(bytes); return; }
        var temp = Path.Combine(Path.GetTempPath(), "ResourceSplitter-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var file = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var chunk in pointer.Chunks)
            {
                byte[] part;
                if (root != null)
                {
                    var path = SafePath(root, Store + chunk.Oid);
                    part = File.Exists(path) ? File.ReadAllBytes(path) : Git(null, "-C", root, "cat-file", "blob", chunk.Oid);
                }
                else part = Git(null, "cat-file", "blob", chunk.Oid);
                if (part.Length != chunk.Size || Hash(part) != chunk.Sha256) throw new Exception("Missing/corrupt chunk: " + chunk.Oid);
                hash.AppendData(part); file.Write(part);
            }
            if (file.Length != pointer.Size || Convert.ToHexString(hash.GetHashAndReset()) != pointer.Sha256) throw new Exception("Restored asset hash mismatch.");
            file.Position = 0; file.CopyTo(destination);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    static void Prepare()
    {
        var chunks = new Dictionary<string, Chunk>();
        var entries = Encoding.UTF8.GetString(Git(null, "ls-files", "--stage", "-z")).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var existing = new HashSet<string>();
        var candidates = new Dictionary<string, List<string>>();
        foreach (var entry in entries)
        {
            var tab = entry.IndexOf('\t'); var fields = entry[..tab].Split(' '); var path = entry[(tab + 1)..];
            if (fields[2] != "0") throw new Exception("Resolve merge conflicts before committing.");
            if (path.StartsWith(Store, StringComparison.Ordinal)) { existing.Add(path); continue; }
            if (fields[0] == "160000") continue;
            if (!candidates.TryGetValue(fields[1], out var paths)) candidates[fields[1]] = paths = new List<string>();
            paths.Add(path);
        }
        var report = candidates.Count == 0 ? "" : Encoding.UTF8.GetString(Git(Encoding.ASCII.GetBytes(string.Join("\n", candidates.Keys) + "\n"), "cat-file", "--batch-check=%(objectname) %(objecttype) %(objectsize)"));
        var small = new List<string>();
        foreach (var item in report.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = item.Split(' ');
            if (fields.Length != 3 || fields[1] != "blob") throw new Exception("Invalid staged blob.");
            var size = long.Parse(fields[2]);
            if (size > Threshold && !(size < 100L * 1024 * 1024 && candidates[fields[0]].All(ToolPath)))
                throw new Exception("Large raw blob is staged: " + string.Join(", ", candidates[fields[0]]) + ". Check attributes and run git add again.");
            if (size <= 1024 * 1024) small.Add(fields[0]);
        }
        foreach (var p in Pointers(small).Values)
            foreach (var chunk in p.Chunks)
            {
                var cache = SafePath(Text("rev-parse", "--show-toplevel"), Store + chunk.Oid);
                if (File.Exists(cache))
                {
                    var data = File.ReadAllBytes(cache);
                    if (data.Length != chunk.Size || Hash(data) != chunk.Sha256 || BlobId(data, chunk.Oid.Length == 64) != chunk.Oid) throw new Exception("Corrupt chunk cache.");
                    var saved = Encoding.ASCII.GetString(Git(data, "hash-object", "-w", "--stdin")).Trim();
                    if (saved != chunk.Oid) throw new Exception("Chunk uses a different Git object format.");
                }
                if (long.Parse(Text("cat-file", "-s", chunk.Oid)) != chunk.Size) throw new Exception("Chunk size mismatch.");
                chunks[chunk.Oid] = chunk;
            }
        // Update only chunk entries; preserve every user-selected staged asset version.
        var root = Text("rev-parse", "--show-toplevel");
        var directory = Path.Combine(root, Store);
        foreach (var folder in new[] { Path.Combine(root, ".resourcesplitter"), directory })
            if (Directory.Exists(folder) && (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) throw new Exception("Chunk directory cannot be a symbolic link/junction.");
        Directory.CreateDirectory(directory);
        foreach (var chunk in chunks.Values)
        {
            var path = Path.Combine(directory, chunk.Oid);
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new Exception("Chunk cannot be a symbolic link.");
            var bytes = File.Exists(path) ? File.ReadAllBytes(path) : Git(null, "cat-file", "blob", chunk.Oid);
            if (bytes.Length != chunk.Size || Hash(bytes) != chunk.Sha256) throw new Exception("Corrupt chunk cache: " + chunk.Oid);
            if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
            Text("update-index", "--add", "--cacheinfo", "100644", chunk.Oid, Store + chunk.Oid);
        }
        foreach (var path in existing.Where(p => !chunks.ContainsKey(p[Store.Length..]))) Text("update-index", "--force-remove", "--", path);
        Console.Error.WriteLine("ResourceSplitter: verified and staged " + chunks.Count + " chunks.");
    }
    static void CheckPush()
    {
        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 4 || fields[1].All(c => c == '0')) continue;
            if (!Oid(fields[1]) || !Oid(fields[3])) throw new Exception("Invalid push ref.");
            var args = new List<string> { "rev-list", "--objects", fields[1] };
            // Validate the complete pushed history, including pre-existing raw large blobs.
            var objectLines = Text(args.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var tools = objectLines.Where(s => s.IndexOf(' ') is var space && space > 0 && ToolPath(s[(space + 1)..])).Select(s => s.Split(' ')[0]).ToHashSet();
            var objects = objectLines.Select(s => s.Split(' ')[0]).Distinct().ToList();
            var reachable = objects.ToHashSet();
            var report = Encoding.UTF8.GetString(Git(Encoding.ASCII.GetBytes(string.Join("\n", objects) + "\n"), "cat-file", "--batch-check=%(objectname) %(objecttype) %(objectsize)"));
            var small = new List<string>();
            foreach (var item in report.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var f = item.Split(' '); if (f.Length != 3 || f[1] != "blob") continue;
                var size = long.Parse(f[2]);
                if (size > Threshold && !(size < 100L * 1024 * 1024 && tools.Contains(f[0]))) throw new Exception("Push contains a raw blob exceeding 50 MiB: " + f[0] + ". Existing history must be migrated separately.");
                if (size > 1024 * 1024) continue;
                small.Add(f[0]);
            }
            foreach (var p in Pointers(small).Values)
                if (p.Chunks.Any(c => !reachable.Contains(c.Oid))) throw new Exception("Push contains a pointer without committed chunks. Run ResourceSplitter.exe git-prepare before committing.");
        }
    }
    static void Install(string root)
    {
        Directory.SetCurrentDirectory(Path.GetFullPath(root));
        var actual = Path.GetFullPath(Text("rev-parse", "--show-toplevel"));
        if (!actual.Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), FileSystemPaths.Comparison)) throw new Exception("Specify the Git repository root.");
        var hooks = Path.GetFullPath(Text("rev-parse", "--git-path", "hooks"));
        Directory.CreateDirectory(hooks);
        foreach (var name in new[] { "pre-commit", "pre-push" })
        {
            var path = Path.Combine(hooks, name);
            if (File.Exists(path) && !File.ReadAllText(path).Contains("# ResourceSplitter managed hook")) throw new Exception("Existing hook must be integrated manually: " + path);
        }
        var tool = Path.Combine(actual, ".resourcesplitter", InstalledToolName);
        if (Directory.Exists(Path.GetDirectoryName(tool)) && (File.GetAttributes(Path.GetDirectoryName(tool)!) & FileAttributes.ReparsePoint) != 0) throw new Exception("Tool directory cannot be a symbolic link/junction.");
        Directory.CreateDirectory(Path.GetDirectoryName(tool)!);
        var source = Environment.ProcessPath ?? throw new Exception("Executable path unavailable.");
        if (!source.Equals(tool, FileSystemPaths.Comparison)) File.Copy(source, tool, true);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(tool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        var attributes = Path.Combine(actual, ".gitattributes");
        var old = File.Exists(attributes) ? File.ReadAllText(attributes) : "";
        if (!old.Contains("# ResourceSplitter automatic storage")) File.AppendAllText(attributes,
            "\n# ResourceSplitter automatic storage\n* filter=resourcesplitter -text\n*.meta -filter\n.gitattributes -filter\n.gitignore -filter\n.resourcesplitter/** -filter -text -diff -merge\nResourceSplitter.* -filter\nTools/ResourceSplitter/** -filter\n", new UTF8Encoding(false));
        var ignore = Path.Combine(actual, ".gitignore");
        if (!File.Exists(ignore) || !File.ReadAllText(ignore).Contains("/.resourcesplitter/objects/"))
            File.AppendAllText(ignore, "\n# ResourceSplitter chunk cache (required chunks are staged by the hook)\n/.resourcesplitter/objects/\n", new UTF8Encoding(false));
        Text("config", "--local", "filter.resourcesplitter.clean", "\".resourcesplitter/" + InstalledToolName + "\" git-clean");
        Text("config", "--local", "filter.resourcesplitter.smudge", "\".resourcesplitter/" + InstalledToolName + "\" git-smudge");
        Text("config", "--local", "filter.resourcesplitter.required", "true");
        foreach (var (name, command) in new[] { ("pre-commit", "git-prepare"), ("pre-push", "git-check-push") })
        {
            var hook = Path.Combine(hooks, name);
            File.WriteAllText(hook, "#!/bin/sh\n# ResourceSplitter managed hook\nexec \".resourcesplitter/" + InstalledToolName + "\" " + command + "\n", new UTF8Encoding(false));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        // On first clone Git has not yet installed the local filter. Restore only
        // exact pointer files, leaving existing edits and missing files untouched.
        foreach (var relative in Encoding.UTF8.GetString(Git(null, "ls-files", "-z")).Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var path = Path.GetFullPath(Path.Combine(actual, relative));
            if (!path.StartsWith(actual + Path.DirectorySeparatorChar, FileSystemPaths.Comparison)) throw new Exception("Invalid tracked path.");
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) continue;
            var current = File.ReadAllBytes(path);
            if (Parse(current) == null) continue;
            var indexed = Git(null, "show", ":" + relative);
            if (!indexed.AsSpan().SequenceEqual(current)) continue;
            var ancestor = path;
            while (ancestor.Length > actual.Length)
            {
                if ((File.GetAttributes(ancestor) & FileAttributes.ReparsePoint) != 0) throw new Exception("Cannot restore through a symbolic link/junction.");
                ancestor = Path.GetDirectoryName(ancestor)!;
            }
            var temp = path + ".restore-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var output = File.Create(temp)) Smudge(new MemoryStream(current), output);
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(current)) throw new Exception("File changed during installation: " + relative);
                File.Move(temp, path, true);
                // The blob stays identical; refresh the index's worktree size/stat
                // after changing its on-disk representation from pointer to asset.
                Text("add", "--", relative);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        Console.WriteLine("자동화 설치 완료. git add --renormalize . 후 설정과 도구를 함께 커밋하세요. 새 PC에서도 git-install을 한 번 실행하세요.");
    }
    static void InstallationStatus(string root)
    {
        Directory.SetCurrentDirectory(Path.GetFullPath(root));
        string actual;
        try { actual = Path.GetFullPath(Text("rev-parse", "--show-toplevel")); }
        catch { Console.WriteLine("not-repository"); return; }
        if (!actual.Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), FileSystemPaths.Comparison)) { Console.WriteLine("not-root"); return; }
        string Config(string key) { try { return Text("config", "--local", "--get", key); } catch { return ""; } }
        var clean = Config("filter.resourcesplitter.clean");
        var smudge = Config("filter.resourcesplitter.smudge");
        var hooks = Path.GetFullPath(Text("rev-parse", "--git-path", "hooks"));
        var tool = Path.Combine(actual, ".resourcesplitter", InstalledToolName);
        var attributes = Path.Combine(actual, ".gitattributes");
        bool Hook(string name, string command) => File.Exists(Path.Combine(hooks, name)) && File.ReadAllText(Path.Combine(hooks, name)).Replace("\r\n", "\n") == "#!/bin/sh\n# ResourceSplitter managed hook\nexec \".resourcesplitter/" + InstalledToolName + "\" " + command + "\n";
        var installed = File.Exists(tool) && clean == "\".resourcesplitter/" + InstalledToolName + "\" git-clean" && smudge == "\".resourcesplitter/" + InstalledToolName + "\" git-smudge" && Config("filter.resourcesplitter.required") == "true" && Hook("pre-commit", "git-prepare") && Hook("pre-push", "git-check-push") && File.Exists(attributes) && File.ReadAllText(attributes).Contains("# ResourceSplitter automatic storage");
        Console.WriteLine(installed ? "installed" : clean.Length > 0 || smudge.Length > 0 ? "incomplete" : "not-installed");
    }
    public static int Run(string[] args)
    {
        switch (args[0])
        {
            case "git-status": InstallationStatus(args.Length > 1 ? args[1] : Directory.GetCurrentDirectory()); break;
            case "git-install": Install(args.Length > 1 ? args[1] : Directory.GetCurrentDirectory()); break;
            case "git-clean": Clean(); break;
            case "git-smudge": Smudge(Console.OpenStandardInput(), Console.OpenStandardOutput()); break;
            case "git-prepare": Prepare(); break;
            case "git-check-push": CheckPush(); break;
            default: throw new Exception("Unknown Git integration command.");
        }
        return 0;
    }
}
