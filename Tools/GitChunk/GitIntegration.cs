using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

// The original path stores a small pointer in Git; chunk blobs are ordinary Git files.
// Reading chunks by blob ID makes checkout independent of file checkout order.
static partial class GitIntegration
{
    const long Threshold = 50L * 1024 * 1024;
    const int ChunkSize = 49 * 1024 * 1024;
    const string Prefix = ProjectIdentity.PointerPrefix;
    const string Store = ProjectIdentity.Objects;
    static string InstalledToolName => OperatingSystem.IsWindows() ? "GitChunk.exe" : "GitChunk-" + (OperatingSystem.IsLinux() ? "linux-" : "osx-") + (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64");
    static string Quote(string path) => "'" + path.Replace('\\', '/').Replace("'", "'\\''") + "'";
    static string LegacyRuntimeTool() => Path.Combine(Path.GetFullPath(Text("rev-parse", "--git-path", ProjectIdentity.Id)), InstalledToolName);
    static string RuntimeTool()
    {
        try { var configured = Text("config", "--local", "--get", "gitchunk.executable"); if (Path.IsPathFullyQualified(configured)) return configured; } catch { }
        return LegacyRuntimeTool();
    }
    static void MigrateLegacyStorage(string root)
    {
        var legacy = SafePath(root, ProjectIdentity.LegacyStorage);
        if (!Directory.Exists(legacy)) return;
        var oldObjects = SafePath(root, ProjectIdentity.LegacyStorage + "/objects");
        if (Directory.Exists(oldObjects)) foreach (var file in Directory.EnumerateFiles(oldObjects)) {
            var oid = Path.GetFileName(file);
            if (!Oid(oid)) continue;
            var source = SafePath(root, ProjectIdentity.LegacyObjects + oid);
            var data = File.ReadAllBytes(source);
            if (BlobId(data, oid.Length == 64) != oid) throw new Exception("Corrupt legacy chunk: " + oid + ". Legacy storage was preserved.");
            var target = SafePath(root, Store + oid); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target)) {
                if (!File.ReadAllBytes(target).AsSpan().SequenceEqual(data)) throw new Exception("Conflicting chunk: " + oid);
            } else File.WriteAllBytes(target, data);
            if (BlobId(File.ReadAllBytes(target), oid.Length == 64) != oid) throw new Exception("Migrated chunk verification failed: " + oid);
        }
        // Preserve the complete legacy directory (including unknown files) in
        // Git's local metadata. Only its visible working-tree name disappears.
        var runtimeDirectory = Path.GetDirectoryName(RuntimeTool())!;
        var backup = Path.GetFullPath(Path.Combine(runtimeDirectory, "legacy-storage-" + Guid.NewGuid().ToString("N")));
        if (!backup.StartsWith(Path.GetFullPath(runtimeDirectory) + Path.DirectorySeparatorChar, FileSystemPaths.Comparison)) throw new Exception("Invalid legacy backup path.");
        Directory.CreateDirectory(runtimeDirectory);
        try { Directory.Move(legacy, backup); Console.WriteLine("Legacy storage migrated to .gitchunk; backup retained in Git metadata."); }
        catch (IOException) { Console.WriteLine(("Legacy chunks copied and verified. Close any legacy app, then reinstall to archive ." + ProjectIdentity.LegacyId + ".")); }
    }
    static bool ToolPath(string path) => path is "GitChunk-linux-x64" or "GitChunk-linux-arm64" or ".gitchunk/GitChunk-linux-x64" or ".gitchunk/GitChunk-linux-arm64" or "GitChunk.exe" or ".gitchunk/GitChunk.exe" or "GitChunk-osx-arm64" or "GitChunk-osx-x64" or ".gitchunk/GitChunk-osx-arm64" or ".gitchunk/GitChunk-osx-x64" or (ProjectIdentity.LegacyName + ".exe") or ("." + ProjectIdentity.LegacyId + "/" + ProjectIdentity.LegacyName + ".exe") or (ProjectIdentity.LegacyName + "-osx-arm64") or (ProjectIdentity.LegacyName + "-osx-x64") or ("." + ProjectIdentity.LegacyId + "/" + ProjectIdentity.LegacyName + "-osx-arm64") or ("." + ProjectIdentity.LegacyId + "/" + ProjectIdentity.LegacyName + "-osx-x64");
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
        if (!bytes.AsSpan().StartsWith(prefix)) prefix = Encoding.UTF8.GetBytes(ProjectIdentity.LegacyPointerPrefix);
        if (!bytes.AsSpan().StartsWith(prefix)) return null;
        if (bytes.Length > 1024 * 1024) throw new Exception("Pointer exceeds size limit.");
        var p = JsonSerializer.Deserialize<Pointer>(bytes.AsSpan(prefix.Length)) ?? throw new Exception("Invalid pointer.");
        if (p.Size <= Threshold || p.Chunks == null || p.Chunks.Count == 0 || p.Chunks.Any(c => !Oid(c.Oid) || c.Size < 1 || c.Size > ChunkSize || !Regex.IsMatch(c.Sha256, "\\A[0-9A-F]{64}\\z"))
            || p.Chunks.Sum(c => (long)c.Size) != p.Size || !Regex.IsMatch(p.Sha256, "\\A[0-9A-F]{64}\\z")) throw new Exception("Invalid pointer entries.");
        return p;
    }
    static void Clean(string? relative)
    {
        // Spool to a private temporary file so memory use is bounded by one chunk.
        var temp = Path.Combine(Path.GetTempPath(), ProjectIdentity.TemporaryPrefix + Guid.NewGuid().ToString("N"));
        try
        {
            using var file = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            Console.OpenStandardInput().CopyTo(file);
            file.Position = 0;
            var output = Console.OpenStandardOutput();
            if (file.Length <= Threshold || (relative != null && !ChunkIgnore.Matches(Text("rev-parse", "--show-toplevel"), relative)))
            {
                // Reject malformed reserved pointers instead of silently accepting them.
                if (file.Length <= 1024 * 1024) { var b = new byte[(int)file.Length]; file.ReadExactly(b);
                    if (Encoding.UTF8.GetString(b).StartsWith("version https://git-lfs.github.com/spec/v1\n")) throw new Exception("LFS pointer is not an original asset. Run git chunk install to convert current LFS files first.");
                    Parse(b);
                    var legacyPrefix = Encoding.UTF8.GetBytes(ProjectIdentity.LegacyPointerPrefix);
                    if (b.AsSpan().StartsWith(legacyPrefix)) {
                        output.Write(Encoding.UTF8.GetBytes(Prefix)); output.Write(b.AsSpan(legacyPrefix.Length));
                    } else output.Write(b); }
                else file.CopyTo(output);
                return;
            }
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var chunks = new List<Chunk>();
            var root = Text("rev-parse", "--show-toplevel");
            var directory = Path.Combine(root, Store);
            if ((Directory.Exists(Path.Combine(root, ProjectIdentity.Storage)) && (File.GetAttributes(Path.Combine(root, ProjectIdentity.Storage)) & FileAttributes.ReparsePoint) != 0)
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
                Text("update-ref", ProjectIdentity.CacheRefs + oid, oid);
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
            if (new[] { ".git", ".svn", ".hg", ProjectIdentity.LegacyStorage, ProjectIdentity.Storage, "ResourceParts" }.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)) continue;
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
        var legacyPrefix = Encoding.UTF8.GetBytes(ProjectIdentity.LegacyPointerPrefix);
        if (count == head.Length && legacyPrefix.AsSpan().StartsWith(head))
        {
            Array.Resize(ref head, legacyPrefix.Length);
            count += source.ReadAtLeast(head.AsSpan(count), head.Length - count, throwOnEndOfStream: false);
            prefix = legacyPrefix;
        }
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
        var temp = Path.Combine(Path.GetTempPath(), ProjectIdentity.TemporaryPrefix + Guid.NewGuid().ToString("N"));
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
                    if (!File.Exists(path)) path = SafePath(root, ProjectIdentity.LegacyObjects + chunk.Oid);
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
            if (path.StartsWith(Store, StringComparison.Ordinal) || path.StartsWith(ProjectIdentity.LegacyObjects, StringComparison.Ordinal)) { existing.Add(path); continue; }
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
            if (size >= 100L * 1024 * 1024 || (size > Threshold && candidates[fields[0]].Any(path => !ToolPath(path) && ChunkIgnore.Matches(Text("rev-parse", "--show-toplevel"), path))))
                throw new Exception("Large raw blob is staged: " + string.Join(", ", candidates[fields[0]]) + ". Check attributes and run git add again.");
            if (size <= 1024 * 1024) small.Add(fields[0]);
        }
        var pointers = Pointers(small);
        foreach (var p in pointers.Values)
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
        foreach (var folder in new[] { Path.Combine(root, ProjectIdentity.Storage), directory })
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
        foreach (var path in existing.Where(p => !p.StartsWith(Store, StringComparison.Ordinal) || !chunks.ContainsKey(p[Store.Length..]))) Text("update-index", "--force-remove", "--", path);
        WriteChunkList(root, pointers.SelectMany(pair => candidates[pair.Key].Select(path => (path, pair.Value))), true);
        Console.Error.WriteLine("Git Chunk: verified and staged " + chunks.Count + " chunks.");
    }
    // Informational only: restoration continues to use the original pointer format.
    static void WriteChunkList(string root, IEnumerable<(string Path, Pointer Pointer)> files, bool stage)
    {
        var text = new StringBuilder("# Generated from the Git index. Do not edit.\n# Chunk order for each original file; restoration uses its pointer.\n");
        foreach (var file in files.OrderBy(x => x.Path, StringComparer.Ordinal))
        {
            text.Append('\n').Append('[').Append(JsonSerializer.Serialize(file.Path, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })).Append("]\n");
            for (int i = 0; i < file.Pointer.Chunks.Count; i++)
                text.Append((i + 1).ToString("D6")).Append(" = ").Append(file.Pointer.Chunks[i].Oid).Append('\n');
        }
        var target = ChunkIgnore.PathFor(root, "chunks.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temporary, text.ToString(), new UTF8Encoding(false)); File.Move(temporary, target, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        if (stage) Text("add", "--", ".gitchunk/chunks.txt");
    }
    static void RefreshChunkList(string root)
    {
        var entries = Encoding.UTF8.GetString(Git(null, "ls-files", "--stage", "-z")).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var files = new List<(string Path, string Oid)>();
        foreach (var entry in entries)
        {
            var tab = entry.IndexOf('\t'); var fields = entry[..tab].Split(' '); var path = entry[(tab + 1)..];
            if (fields[2] != "0") throw new Exception("Resolve merge conflicts before updating the chunk list.");
            if (fields[0] != "100644" && fields[0] != "100755") continue;
            if (path.StartsWith(ProjectIdentity.Storage + "/", StringComparison.Ordinal) || path.StartsWith(ProjectIdentity.LegacyStorage + "/", StringComparison.Ordinal)) continue;
            files.Add((path, fields[1]));
        }
        var ids = files.Select(x => x.Oid).Distinct().ToArray();
        var report = ids.Length == 0 ? "" : Encoding.UTF8.GetString(Git(Encoding.ASCII.GetBytes(string.Join("\n", ids) + "\n"), "cat-file", "--batch-check=%(objectname) %(objecttype) %(objectsize)"));
        var small = report.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(' ')).Where(x => x.Length == 3 && x[1] == "blob" && long.Parse(x[2]) <= 1024 * 1024).Select(x => x[0]);
        var pointers = Pointers(small);
        WriteChunkList(root, files.Where(x => pointers.ContainsKey(x.Oid)).Select(x => (x.Path, pointers[x.Oid])), false);
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
                if (size >= 100L * 1024 * 1024) throw new Exception("Push contains a raw blob of 100 MiB or more: " + f[0] + ". Existing history must be migrated separately.");
                if (size > 1024 * 1024) continue;
                small.Add(f[0]);
            }
            foreach (var p in Pointers(small).Values)
                if (p.Chunks.Any(c => !reachable.Contains(c.Oid))) throw new Exception("Push contains a pointer without committed chunks. Run GitChunk.exe git-prepare before committing.");
        }
    }
    static void InstallCore(string root)
    {
        Directory.SetCurrentDirectory(Path.GetFullPath(root));
        var actual = Path.GetFullPath(Text("rev-parse", "--show-toplevel"));
        if (!actual.Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), FileSystemPaths.Comparison)) throw new Exception("Specify the Git repository root.");
        var hooks = Path.GetFullPath(Text("rev-parse", "--git-path", "hooks"));
        Directory.CreateDirectory(hooks);
        foreach (var name in new[] { "pre-commit", "pre-push" })
        {
            var path = Path.Combine(hooks, name);
            if (File.Exists(path) && !File.ReadAllText(path).Contains(ProjectIdentity.ManagedHook) && !File.ReadAllText(path).Contains(ProjectIdentity.LegacyManagedHook) && !StandardLfsHook(File.ReadAllText(path), name)) throw new Exception("Existing hook must be integrated manually: " + path);
        }
        var runtime = SharedTool.Executable();
        ChunkIgnore.EnsureDefaults(actual);
        Text("config", "--local", "gitchunk.executable", runtime);
        var attributes = Path.Combine(actual, ".gitattributes");
        var old = File.Exists(attributes) ? File.ReadAllText(attributes) : "";
        if (!old.Contains(ProjectIdentity.AutomaticStorage)) File.AppendAllText(attributes,
            ProjectIdentity.Attributes, new UTF8Encoding(false));
        else
        {
            var updated = old.Replace("\r\n", "\n");
            updated = Regex.Replace(updated, @"(?m)^\.gitchunk/rules\.txt -filter text eol=lf diff\n?", "");
            if (!updated.Contains(".chunkignore -filter text eol=lf diff")) updated = updated.TrimEnd('\n') + "\n.chunkignore -filter text eol=lf diff\n";
            updated = Regex.Replace(updated, @"(?m)^\.gitchunk/presets/\*\* -filter text eol=lf diff\n?", "");
            if (!updated.Contains(".gitchunk/chunks.txt -filter text eol=lf diff"))
                updated = updated.TrimEnd('\n') + "\n.gitchunk/chunks.txt -filter text eol=lf diff\n";
            if (updated != old) File.WriteAllText(attributes, updated, new UTF8Encoding(false));
        }
        var ignore = Path.Combine(actual, ".gitignore");
        if (!File.Exists(ignore) || !File.ReadAllText(ignore).Contains("/.gitchunk/objects/"))
            File.AppendAllText(ignore, "\n# GitChunk chunk cache (required chunks are staged by the hook)\n/.gitchunk/objects/\n", new UTF8Encoding(false));
        // Historical .gitattributes files still refer to the legacy driver.
        foreach (var driver in new[] { ProjectIdentity.Id, ProjectIdentity.LegacyId }) {
            Text("config", "--local", "filter." + driver + ".clean", Quote(runtime) + " git-clean %f");
            Text("config", "--local", "filter." + driver + ".smudge", Quote(runtime) + " git-smudge");
            Text("config", "--local", "filter." + driver + ".required", "true");
        }
        foreach (var (name, command) in new[] { ("pre-commit", "git-prepare"), ("pre-push", "git-check-push") })
        {
            var hook = Path.Combine(hooks, name);
            if (File.Exists(hook) && StandardLfsHook(File.ReadAllText(hook), name)) {
                var backup = Path.GetFullPath(Text("rev-parse", "--git-path", ProjectIdentity.Id + "/" + name + ".lfs-backup"));
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!); File.Copy(hook, backup, true);
            }
            File.WriteAllText(hook, "#!/bin/sh\n# GitChunk managed hook\nexec " + Quote(runtime) + " " + command + "\n", new UTF8Encoding(false));
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
        MigrateLegacyStorage(actual);
        RefreshChunkList(actual);
        var presets = ChunkIgnore.PathFor(actual, "presets");
        if (Directory.Exists(presets)) {
            var backup = Path.Combine(Text("rev-parse", "--absolute-git-dir"), ProjectIdentity.Id, "presets-backup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            Directory.Move(presets, backup);
            Console.WriteLine("Old presets backed up: " + backup);
        }
        // Retire old repository-local binaries only after the shared hooks/filters work.
        var retired = new[] { ProjectIdentity.Storage, ProjectIdentity.LegacyStorage }
            .SelectMany(folder => Directory.Exists(Path.Combine(actual, folder)) ? Directory.GetFiles(Path.Combine(actual, folder)) : Array.Empty<string>())
            .Where(file => ToolPath(Path.GetRelativePath(actual, file).Replace('\\', '/'))).Append(LegacyRuntimeTool());
        foreach (var file in retired.Distinct(FileSystemPaths.Comparer)) if (File.Exists(file)) {
            var backup = Path.GetFullPath(Text("rev-parse", "--git-path", "gitchunk/retired-" + Guid.NewGuid().ToString("N") + "/" + Path.GetFileName(file)));
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!); File.Move(file, backup);
            if (file.StartsWith(actual + Path.DirectorySeparatorChar + ProjectIdentity.Storage + Path.DirectorySeparatorChar, FileSystemPaths.Comparison)
                || file.StartsWith(actual + Path.DirectorySeparatorChar + ProjectIdentity.LegacyStorage + Path.DirectorySeparatorChar, FileSystemPaths.Comparison))
                Text("update-index", "--force-remove", "--", Path.GetRelativePath(actual, file).Replace('\\', '/'));
        }
        Console.WriteLine("Git Chunk configured using shared executable: " + runtime);
        Console.WriteLine("Run git add -u, then git add --renormalize . and commit the configuration. No executable is copied into this repository.");
    }
    static void InstallationStatus(string root)
    {
        Directory.SetCurrentDirectory(Path.GetFullPath(root));
        string actual;
        try { actual = Path.GetFullPath(Text("rev-parse", "--show-toplevel")); }
        catch { Console.WriteLine("not-repository"); return; }
        if (!actual.Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), FileSystemPaths.Comparison)) { Console.WriteLine("not-root"); return; }
        string Config(string key) { try { return Text("config", "--local", "--get", key); } catch { return ""; } }
        var clean = Config("filter.gitchunk.clean");
        var smudge = Config("filter.gitchunk.smudge");
        var hooks = Path.GetFullPath(Text("rev-parse", "--git-path", "hooks"));
        var tool = Path.Combine(actual, ProjectIdentity.Storage, InstalledToolName);
        var runtime = RuntimeTool();
        var attributes = Path.Combine(actual, ".gitattributes");
        bool Hook(string name, string command) => File.Exists(Path.Combine(hooks, name)) && File.ReadAllText(Path.Combine(hooks, name)).Replace("\r\n", "\n") == "#!/bin/sh\n# GitChunk managed hook\nexec " + Quote(runtime) + " " + command + "\n";
        var installed = File.Exists(runtime) && clean == Quote(runtime) + " git-clean %f" && smudge == Quote(runtime) + " git-smudge" && Config("filter.gitchunk.required") == "true" && Hook("pre-commit", "git-prepare") && Hook("pre-push", "git-check-push") && File.Exists(attributes) && File.ReadAllText(attributes).Contains(ProjectIdentity.AutomaticStorage) && File.Exists(ChunkIgnore.IgnorePath(actual));
        Console.WriteLine(installed ? "installed" : clean.Length > 0 || smudge.Length > 0 ? "incomplete" : Config("filter." + ProjectIdentity.LegacyId + ".clean").Length > 0 ? "legacy" : "not-installed");
    }
    public static int Run(string[] args)
    {
        switch (args[0])
        {
            case "git-ignore-read":
            case "git-rules-read": Console.Write(ChunkIgnore.Load(args[1])); break;
            case "git-ignore-save":
            case "git-rules-save": ChunkIgnore.Save(args[1], Console.In.ReadToEnd()); break;
            case "git-ignore-match":
            case "git-rules-match": Console.WriteLine(ChunkIgnore.Matches(args[1], args[2]) ? "include" : "exclude"); break;
            case "git-status": InstallationStatus(args.Length > 1 ? args[1] : Directory.GetCurrentDirectory()); break;
            case "git-install": Install(args.Length > 1 ? args[1] : Directory.GetCurrentDirectory()); Console.WriteLine("Applied Git Chunk " + ToolUpdate.VersionText + " from " + Environment.ProcessPath); ToolUpdate.Check(); break;
            case "git-uninstall": Uninstall(args.Length > 1 ? args[1] : Directory.GetCurrentDirectory()); break;
            case "git-clean": Clean(args.Length > 1 ? args[1] : null); break;
            case "git-smudge": Smudge(Console.OpenStandardInput(), Console.OpenStandardOutput()); break;
            case "git-prepare": Prepare(); break;
            case "git-check-push": CheckPush(); break;
            default: throw new Exception("Unknown Git integration command.");
        }
        return 0;
    }
}
