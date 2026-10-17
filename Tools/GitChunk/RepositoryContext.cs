// Repository-local copies stay bound to their owning worktree, regardless of cwd.
static class RepositoryContext
{
    public static string? FromExecutable(string executable)
    {
        var directory = Directory.GetParent(Path.GetFullPath(executable));
        if (directory == null) return null;
        if (directory.Name.Equals(ProjectIdentity.Storage, FileSystemPaths.Comparison))
            return directory.Parent?.FullName;
        if (!directory.Name.Equals(ProjectIdentity.Id, FileSystemPaths.Comparison)) return null;
        var metadata = directory.Parent;
        if (metadata == null) return null;
        if (metadata.Name.Equals(".git", FileSystemPaths.Comparison)) return metadata.Parent?.FullName;
        // Linked worktrees keep their runtime under .git/worktrees/<name>/gitchunk.
        var backlink = Path.Combine(metadata.FullName, "gitdir");
        if (File.Exists(backlink))
        {
            var gitFile = File.ReadAllText(backlink).Trim();
            if (Path.IsPathFullyQualified(gitFile) && Path.GetFileName(gitFile).Equals(".git", FileSystemPaths.Comparison))
                return Path.GetDirectoryName(gitFile);
        }
        return null;
    }
}
