using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

partial class MainWindow : Window
{
    bool busy;
    string mode = "git-install";
    public MainWindow()
    {
        InitializeComponent();
        FolderPath.Text = Directory.GetCurrentDirectory();
        GitMode.IsChecked = true;
        Append("프로젝트 폴더와 작업을 선택하세요.");
    }
    void Browse(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "프로젝트 폴더 선택", Multiselect = false };
        if (Directory.Exists(FolderPath.Text)) picker.InitialDirectory = FolderPath.Text;
        if (picker.ShowDialog(this) == true) FolderPath.Text = picker.FolderName;
    }
    void ModeChanged(object sender, RoutedEventArgs e)
    {
        if (RunButton == null || ModeHint == null) return;
        mode = (string)((RadioButton)sender).Tag;
        (RunButton.Content, ModeHint.Text) = mode switch
        {
            "join" => ("복원 시작  →", "SHA-256 검증 후 원본을 복원합니다. 데이터 조각은 유지됩니다."),
            "git-install" => ("자동화 설치  →", "Git 저장소의 루트 폴더를 선택하세요. 설치 후 초기 설정을 한 번 커밋해야 합니다."),
            _ => ("분할 시작  →", "50 MiB 초과 파일만 분할합니다. 작업 전 에디터를 닫고, 에디터를 열기 전에 원본을 복원하세요.")
        };
    }
    void Append(string text)
    {
        Log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        Log.ScrollToEnd();
    }
    async Task ReadLog(StreamReader reader)
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var message = line;
            await Dispatcher.InvokeAsync(() => Append(message));
        }
    }
    async void Execute(object sender, RoutedEventArgs e)
        => await RunSelected();
    async Task RunSelected()
    {
        if (busy) return;
        string path;
        try
        {
            path = Path.GetFullPath(FolderPath.Text.Trim().Trim('"'));
            if (!Directory.Exists(path)) throw new Exception("존재하는 프로젝트 폴더를 선택해 주세요.");
        }
        catch (Exception ex) { StatusText.Text = "폴더 확인 필요"; Append(ex.Message); return; }
        busy = true;
        RunButton.IsEnabled = BrowseButton.IsEnabled = Modes.IsEnabled = FolderPath.IsEnabled = false;
        Progress.Visibility = Visibility.Visible; Progress.IsIndeterminate = true;
        StatusText.Text = "처리 중…";
        var title = mode == "split" ? "파일 분할" : mode == "join" ? "원본 복원" : "Git 자동화 설치";
        Append($"{title} 시작 · {path}");
        var timer = Stopwatch.StartNew();
        try
        {
            var info = new ProcessStartInfo(Environment.ProcessPath ?? throw new Exception("EXE 경로를 확인할 수 없습니다."))
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = path,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            info.ArgumentList.Add(mode); info.ArgumentList.Add(path);
            using var process = Process.Start(info) ?? throw new Exception("작업을 시작할 수 없습니다.");
            var stdout = ReadLog(process.StandardOutput); var stderr = ReadLog(process.StandardError);
            await process.WaitForExitAsync(); await Task.WhenAll(stdout, stderr);
            if (process.ExitCode != 0)
            {
                StatusText.Text = "작업 실패 · 기록을 확인해 주세요";
                Append("작업이 오류로 중단되었습니다. 위 오류를 확인한 뒤 다시 시도해 주세요.");
            }
            else
            {
                StatusText.Text = $"{title} 완료";
                Append($"완료 · {timer.Elapsed.TotalSeconds:F1}초");
                if (mode == "git-install")
                    Append("최초 설치 후 저장소에서 실행할 명령:\ngit add --renormalize .\ngit add .gitattributes .gitignore .resourcesplitter/ResourceSplitter.exe\ngit commit -m \"Enable automatic resource splitting\"");
            }
        }
        catch (Exception ex) { StatusText.Text = "작업 실패"; Append(ex.Message); }
        finally
        {
            busy = false;
            RunButton.IsEnabled = BrowseButton.IsEnabled = Modes.IsEnabled = FolderPath.IsEnabled = true;
            Progress.IsIndeterminate = false; Progress.Visibility = Visibility.Collapsed;
        }
    }
    void CopyLog(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(Log.Text); }
        catch (Exception ex) { Append("로그 복사 실패: " + ex.Message); }
    }
    void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    void OnDrop(object sender, DragEventArgs e)
    {
        if (busy || e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length != 1) return;
        if (Directory.Exists(paths[0])) FolderPath.Text = paths[0];
        else Append("파일 대신 프로젝트 폴더를 끌어다 놓아 주세요.");
        e.Handled = true;
    }
    void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!busy) return;
        e.Cancel = true;
        StatusText.Text = "처리가 완료되면 창을 닫을 수 있습니다";
    }
    // Render the real WPF controls for visual QA without leaving a visible window.
    internal static int Preview(string path)
    {
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var app = new Application();
        var window = new MainWindow { WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
        window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(() =>
        {
            var content = (FrameworkElement)window.Content;
            content.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var output = File.Create(Path.GetFullPath(path))) encoder.Save(output);
            window.Close();
        }, DispatcherPriority.ApplicationIdle);
        return app.Run(window);
    }
    internal static int Smoke(string folder)
    {
        var app = new Application();
        var window = new MainWindow { WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
        window.Loaded += async (_, _) =>
        {
            try
            {
                var file = Path.Combine(folder, "large.bin");
                static string FileHash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)); }
                var original = FileHash(file);
                if (window.GitMode.IsChecked != true || window.mode != "git-install") throw new Exception("Recommended default is not selected.");
                window.SplitMode.IsChecked = true;
                window.FolderPath.Text = folder;
                await window.RunSelected();
                if (!window.StatusText.Text.EndsWith("완료") || new FileInfo(file).Length > 1024 || !window.RunButton.IsEnabled) throw new Exception("GUI split failed.");
                window.JoinMode.IsChecked = true;
                await window.RunSelected();
                if (!window.StatusText.Text.EndsWith("완료") || FileHash(file) != original || window.busy) throw new Exception("GUI restore failed.");
                window.GitMode.IsChecked = true;
                if ((string)window.RunButton.Content != "자동화 설치  →") throw new Exception("GUI mode selection failed.");
                window.FolderPath.Text = Path.Combine(folder, "missing-folder");
                await window.RunSelected();
                if (window.StatusText.Text != "폴더 확인 필요") throw new Exception("GUI validation failed.");
                Console.WriteLine("PASS: real GUI split/restore workers, hash, mode switching, folder validation, control state.");
                app.Shutdown(0);
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message + "\n" + window.Log.Text); app.Shutdown(1); }
        };
        return app.Run(window);
    }
}

static class ConsoleHost
{
    [DllImport("kernel32.dll")] static extern bool AttachConsole(uint processId);
    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int handle);
    [DllImport("kernel32.dll")] static extern uint GetFileType(IntPtr handle);
    internal static void AttachIfNeeded()
    {
        // Git filters and GUI workers inherit binary pipes. Never replace them.
        var type = GetFileType(GetStdHandle(-11));
        if (type != 3 && type != 1) AttachConsole(uint.MaxValue);
        Console.OutputEncoding = new UTF8Encoding(false);
    }
}
