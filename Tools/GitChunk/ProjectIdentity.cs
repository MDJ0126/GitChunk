// Current names and read-only compatibility identifiers are managed here.
static class ProjectIdentity
{
    public const string Name = "GitChunk";
    public const string Id = "gitchunk";
    public const string Storage = "." + Id;
    public const string Objects = Storage + "/objects/";
    public const string PointerPrefix = Id + "-pointer-v1\n";
    public const string CacheRefs = "refs/" + Id + "/cache/";
    public const string TemporaryPrefix = Name + "-";
    public const string SettingsFile = Name + ".settings.json";
    public const string ManifestFile = Name + ".manifest.json";
    public const string LegacyName = "ResourceSplitter";
    public const string LegacyId = "resourcesplitter";
    public const string LegacyStorage = "." + LegacyId;
    public const string LegacyObjects = LegacyStorage + "/objects/";
    public const string LegacyPointerPrefix = LegacyId + "-pointer-v1\n";
    public const string ManagedHook = "# " + Name + " managed hook";
    public const string LegacyManagedHook = "# " + LegacyName + " managed hook";
    public const string AutomaticStorage = "# " + Name + " automatic storage";
    public const string LegacyAutomaticStorage = "# " + LegacyName + " automatic storage";
    public const string SourceDirectory = "Tools/" + Name + "/";
    public const string LegacySourceDirectory = "Tools/" + LegacyName + "/";
    public const string LegacySettingsFile = LegacyName + ".settings.json";
    public const string LegacyManifestFile = LegacyName + ".manifest.json";
    public const string FilterPattern = @"\sfilter=(?:" + Id + "|" + LegacyId + @")";
    public const string Attributes = "\n" + AutomaticStorage + "\n* filter=" + Id + " -text\n"
        + "*.meta -filter\n.gitattributes -filter\n.gitignore -filter\n"
        + Storage + "/** -filter -text -diff -merge\n.chunkignore -filter text eol=lf diff\n"
        + Storage + "/chunks.txt -filter text eol=lf diff\n"
        + LegacyStorage + "/** -filter -text -diff -merge\n"
        + Name + ".* -filter\n" + LegacyName + ".* -filter\n" + SourceDirectory + "** -filter\n";
}
