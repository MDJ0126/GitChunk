using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

partial class MainWindow : Window
{
    bool busy, dirty, loading;
    string installation = "unknown", loadedRoot = "";
    int revision;
    public MainWindow()
    {
        InitializeComponent();
        ApplyState();
        FolderPath.TextChanged += async (_, _) => await RefreshInstallation();
        Activated += async (_, _) => { if (!busy) await RefreshInstallation(); };
        FolderPath.Text = Directory.GetCurrentDirectory();
        Append("Select a repository, save your rules, and install Git Chunk. Existing LFS files are converted and staged automatically; commit or stash tracked changes first. Past LFS history and server storage are preserved.");
    }
    string Root() => Path.TrimEndingDirectorySeparator(Path.GetFullPath(FolderPath.Text.Trim().Trim('"')));
    bool ValidRepository => installation is "installed" or "incomplete" or "legacy" or "not-installed";
    void ApplyState()
    {
        RunButton.Content = installation == "installed" ? "Reinstall this version  →" : installation == "legacy" ? "Upgrade to Git Chunk  →" : "Install Git Chunk  →";
        RunButton.IsEnabled = !busy && ValidRepository;
        UninstallButton.IsEnabled = !busy && installation is "installed" or "incomplete" or "legacy";
        Modes.IsEnabled = !busy && ValidRepository;
        ModeHint.Text = installation switch {
            "installed" => "✓ Installed · Running Git Chunk " + ToolUpdate.Current.ToString(3) + ". Use Update to latest for a newer release.",
            "legacy" => "A legacy installation was found. Upgrade to Git Chunk to migrate its storage.",
            "incomplete" => "Some settings are missing. Reinstall to repair them.",
            "not-installed" => "Not installed · Install to convert current LFS files or restore a fresh clone.",
            "not-root" => "Select the root folder of your Git repository.",
            "not-repository" => "The selected folder is not a Git repository.",
            _ => "Unable to check installation. Verify the folder path and Git installation."
        };
    }
    async Task<string> Command(string command, string root, bool log = false)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = root };
        info.ArgumentList.Add(command); info.ArgumentList.Add(root);
        using var child = Process.Start(info) ?? throw new Exception("Could not start the executable.");
        var output = child.StandardOutput.ReadToEndAsync(); var errors = child.StandardError.ReadToEndAsync();
        await child.WaitForExitAsync(); var stdout = await output; var stderr = await errors;
        if (log) { if (stdout.Length > 0) Append(stdout.Trim()); if (stderr.Length > 0) Append(stderr.Trim()); }
        if (child.ExitCode != 0) throw new Exception(stderr.Trim());
        return stdout.Trim();
    }
    async Task RefreshInstallation()
    {
        var current = ++revision; await Task.Delay(250);
        if (current != revision || busy) return;
        try {
            var root = Root(); if (!Directory.Exists(root)) throw new Exception();
            var state = await Command("git-status", root);
            if (current != revision || busy) return;
            installation = state;
            if (ValidRepository && !root.Equals(loadedRoot, FileSystemPaths.Comparison)) {
                if (dirty) Append("Unsaved rules were not written to the previous folder.");
                LoadRules(root); loadedRoot = root;
            }
        } catch { if (current != revision) return; installation = "unknown"; }
        ApplyState();
    }
    void LoadRules(string root)
    {
        loading = true; try { RulesEditor.Text = ChunkIgnore.Load(root); dirty = false; } finally { loading = false; }
    }
    void RulesChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) { if (!loading && StatusText != null) { dirty = true; StatusText.Text = "Ignore list changed · not saved yet"; } }
    void StoreRules()
    {
        if (!ValidRepository || !Root().Equals(loadedRoot, FileSystemPaths.Comparison)) throw new Exception("Wait for repository validation before saving.");
        ChunkIgnore.EnsureDefaults(Root()); ChunkIgnore.Save(Root(), RulesEditor.Text); dirty = false;
        StatusText.Text = "Ignore list saved";
        Append("Saved .chunkignore. Run git add --renormalize . to apply the policy to existing tracked files.");
    }
    void SaveRules(object sender, RoutedEventArgs e) { try { StoreRules(); } catch (Exception ex) { Append(ex.Message); } }
    async void Browse(object sender, RoutedEventArgs e)
    {
        if (dirty && MessageBox.Show(this, "Discard unsaved rules and switch folders?", "Git Chunk", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        var picker = new OpenFolderDialog { Title = "Select Git repository root", Multiselect = false };
        if (Directory.Exists(FolderPath.Text)) picker.InitialDirectory = FolderPath.Text;
        if (picker.ShowDialog(this) == true) { FolderPath.Text = picker.FolderName; await RefreshInstallation(); }
    }
    async void Execute(object sender, RoutedEventArgs e) => await Install();
    async void UpdateLatest(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        try {
            await RefreshInstallation(); if (!ValidRepository) throw new Exception("Select the repository root.");
            StoreRules(); busy = true; ApplyState(); FolderPath.IsEnabled = BrowseButton.IsEnabled = false;
            Progress.Visibility = Visibility.Visible; Progress.IsIndeterminate = true; StatusText.Text = "Updating to latest release…";
            await Command("update", Root(), true);
            StatusText.Text = "Program and repository updated";
            Append("Future git chunk commands use the updated version. Reopen the app to use the updated Windows interface.");
        } catch (Exception ex) { StatusText.Text = "Update failed"; Append(ex.Message); }
        finally { busy = false; FolderPath.IsEnabled = BrowseButton.IsEnabled = true; Progress.IsIndeterminate = false; Progress.Visibility = Visibility.Collapsed; await RefreshInstallation(); }
    }
    async void Uninstall(object sender, RoutedEventArgs e) => await RemoveInstallation();
    async Task RemoveInstallation()
    {
        if (busy) return;
        try {
            await RefreshInstallation(); if (!ValidRepository) throw new Exception("Select the repository root.");
            busy = true; ApplyState(); FolderPath.IsEnabled = BrowseButton.IsEnabled = false;
            Progress.Visibility = Visibility.Visible; Progress.IsIndeterminate = true; StatusText.Text = "Uninstalling…";
            await Command("git-uninstall", Root(), true);
            dirty = false; loadedRoot = ""; StatusText.Text = "Git Chunk uninstalled";
        } catch (Exception ex) { StatusText.Text = "Uninstall failed"; Append(ex.Message); }
        finally { busy = false; FolderPath.IsEnabled = BrowseButton.IsEnabled = true; Progress.IsIndeterminate = false; Progress.Visibility = Visibility.Collapsed; await RefreshInstallation(); }
    }
    async Task Install()
    {
        if (busy) return;
        try {
            await RefreshInstallation(); if (!ValidRepository) throw new Exception("Select the root folder of your Git repository.");
            StoreRules(); busy = true; ApplyState(); FolderPath.IsEnabled = BrowseButton.IsEnabled = false;
            Progress.Visibility = Visibility.Visible; Progress.IsIndeterminate = true; StatusText.Text = "Installing…";
            await Command("git-install", Root(), true);
            StatusText.Text = "Git Chunk installed";
            Append("After first installation or changing rules:\ngit add -u\ngit add --renormalize .\ngit add .gitattributes .gitignore .gitchunk\ngit commit -m \"Configure Git Chunk\"\nThen commit, push and pull as usual.");
        } catch (Exception ex) { StatusText.Text = "Operation failed"; Append(ex.Message); }
        finally { busy = false; FolderPath.IsEnabled = BrowseButton.IsEnabled = true; Progress.IsIndeterminate = false; Progress.Visibility = Visibility.Collapsed; await RefreshInstallation(); }
    }
    void Append(string text) { Log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}"); Log.ScrollToEnd(); }
    void CopyLog(object sender, RoutedEventArgs e) { try { Clipboard.SetText(Log.Text); } catch (Exception ex) { Append(ex.Message); } }
    void OnDragOver(object sender, DragEventArgs e) { e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    void OnDrop(object sender, DragEventArgs e) { if (!busy && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length == 1 && Directory.Exists(paths[0])) FolderPath.Text = paths[0]; e.Handled = true; }
    void OnClosing(object? sender, CancelEventArgs e) { if (busy) { e.Cancel = true; StatusText.Text = "Wait for the current operation to finish before closing."; } else if (dirty && MessageBox.Show(this, "Discard unsaved rules and close?", "Git Chunk", MessageBoxButton.YesNo) != MessageBoxResult.Yes) e.Cancel = true; }
    internal static int Preview(string path)
    {
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var app = new Application(); var window = new MainWindow { Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false, ShowActivated = false };
        window.Loaded += async (_, _) => { await window.RefreshInstallation(); await window.Dispatcher.InvokeAsync(() => {
            var content = (FrameworkElement)window.Content; content.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var output = File.Create(Path.GetFullPath(path))) encoder.Save(output);
            window.dirty = false; window.Close();
        }, DispatcherPriority.ApplicationIdle); }; return app.Run(window);
    }
    internal static int Smoke(string root)
    {
        var app = new Application(); var window = new MainWindow { Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false, ShowActivated = false };
        window.Loaded += async (_, _) => { try {
            window.FolderPath.Text = root; await window.RefreshInstallation();
            if (window.installation != "not-installed") throw new Exception("Expected fresh repository.");
            window.RulesEditor.Text = "Assets/Small/**\n"; window.StoreRules();
            if (Directory.Exists(ChunkIgnore.PathFor(root, "presets"))) throw new Exception("Unexpected presets.");
            await window.Install(); if (window.installation != "installed") throw new Exception("Install state failed: " + window.Log.Text);
            var reopened = new MainWindow(); reopened.FolderPath.Text = root; await reopened.RefreshInstallation();
            if (reopened.installation != "installed" || reopened.RulesEditor.Text != window.RulesEditor.Text) throw new Exception("Reopen persistence failed.");
            await window.RemoveInstallation();
            if (window.installation != "not-installed" || Directory.Exists(Path.Combine(root, ProjectIdentity.Storage)) || File.Exists(ChunkIgnore.IgnorePath(root))) throw new Exception("GUI uninstall failed: " + window.Log.Text);
            Console.WriteLine("PASS: GUI install, ignore list persistence and uninstall."); window.dirty = false; reopened.dirty = false; app.Shutdown(0);
        } catch (Exception ex) { window.dirty = false; Console.Error.WriteLine(ex); app.Shutdown(1); } }; return app.Run(window);
    }
}
static class ConsoleHost
{
    [DllImport("kernel32.dll")] static extern bool AttachConsole(uint processId);
    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int handle);
    [DllImport("kernel32.dll")] static extern uint GetFileType(IntPtr handle);
    internal static void AttachIfNeeded() { var type = GetFileType(GetStdHandle(-11)); if (type != 3 && type != 1) AttachConsole(uint.MaxValue); Console.OutputEncoding = new UTF8Encoding(false); Console.InputEncoding = new UTF8Encoding(false); }
}
