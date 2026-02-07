using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;

namespace WpfHost;

public partial class HostWindow : Window
{
    private AppLoadContext? _currentContext;
    private FileSystemWatcher? _watcher;
    private int _loadCount = 0;
    private string _appDllPath = "";
    private DateTime _lastReload = DateTime.MinValue;
    
    public HostWindow()
    {
        InitializeComponent();
        
        // Dynamically compute paths based on this assembly's location
        // Get WpfHost.exe directory (e.g., .../WpfHost/bin/Debug/net10.0-windows/win-x64)
        var hostExeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        
        // Navigate up to workspace root: go up from bin/Debug/net10.0-windows/win-x64 -> WpfHost -> workspace
        var workspaceRoot = Path.GetFullPath(Path.Combine(hostExeDir!, "..", "..", "..", "..", ".."));
        
        // Build path to MyWPFApp DLL
        var appDllPath = Path.Combine(workspaceRoot, "MyWPFApp", "bin", "Debug", "net10.0-windows", "win-x64", "MyWPFApp.dll");
        
        // Convert to Wine Z:\ path if on Linux (Z:\ maps to /)
        if (appDllPath.StartsWith("/"))
        {
            appDllPath = "Z:" + appDllPath.Replace("/", "\\");
        }
        
        _appDllPath = appDllPath;
        
        Console.WriteLine($"[Host] WPF Host starting...");
        Console.WriteLine($"[Host] Will load app from: {_appDllPath}");
        
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set up file watcher for hot reload
        SetupFileWatcher();
        
        // Initial load
        LoadApp();
    }
    
    private void SetupFileWatcher()
    {
        try
        {
            var watchDir = Path.GetDirectoryName(_appDllPath);
            if (string.IsNullOrEmpty(watchDir) || !Directory.Exists(watchDir))
            {
                Console.WriteLine($"[Host] Watch directory not found: {watchDir}");
                Console.WriteLine($"[Host] File watching disabled - rebuild manually and click Reload");
                return;
            }
            
            _watcher = new FileSystemWatcher(watchDir)
            {
                Filter = "*.dll",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };
            
            _watcher.Changed += OnDllChanged;
            _watcher.Created += OnDllChanged;
            _watcher.EnableRaisingEvents = true;
            
            Console.WriteLine($"[Host] Watching for changes in: {watchDir}");
            StatusText.Text = " - Watching for changes...";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Host] Failed to set up file watcher: {ex.Message}");
            Console.WriteLine($"[Host] Click Reload button after rebuilding");
        }
    }
    
    private void OnDllChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce - ignore rapid successive events
        if ((DateTime.Now - _lastReload).TotalMilliseconds < 1000)
            return;
            
        if (e.Name?.Contains("MyWPFApp") == true)
        {
            _lastReload = DateTime.Now;
            Console.WriteLine($"[Host] Detected change: {e.Name}");
            
            // Reload on UI thread
            Dispatcher.BeginInvoke(() =>
            {
                StatusText.Text = " - Reloading...";
                // Small delay to let file writes complete
                Task.Delay(500).ContinueWith(_ => 
                    Dispatcher.BeginInvoke(LoadApp));
            });
        }
    }
    
    private void LoadApp()
    {
        try
        {
            Console.WriteLine($"[Host] Loading app assembly...");
            
            // Unload previous context if exists
            UnloadApp();
            
            if (!File.Exists(_appDllPath))
            {
                Console.WriteLine($"[Host] App DLL not found: {_appDllPath}");
                StatusText.Text = " - App DLL not found!";
                ShowMessage("App DLL not found. Build the app first.");
                return;
            }
            
            // Create new load context
            _currentContext = new AppLoadContext(_appDllPath);
            
            // Load the assembly from a copy to avoid file locking
            var tempPath = Path.Combine(Path.GetTempPath(), $"MyWPFApp_{Guid.NewGuid()}.dll");
            File.Copy(_appDllPath, tempPath, true);
            
            var assembly = _currentContext.LoadFromAssemblyPath(tempPath);
            
            // Find the MainWindow type
            var windowType = assembly.GetType("MyWPFApp.MainWindow");
            if (windowType == null)
            {
                Console.WriteLine("[Host] MainWindow type not found in assembly");
                StatusText.Text = " - MainWindow not found!";
                return;
            }
            
            // Create an instance of the window's content
            var window = Activator.CreateInstance(windowType) as Window;
            if (window == null)
            {
                Console.WriteLine("[Host] Failed to create MainWindow instance");
                return;
            }
            
            // Extract the content from the window and host it
            var content = window.Content as UIElement;
            if (content != null)
            {
                // Detach content from original window
                window.Content = null;
                
                // Host it in our container
                AppContainer.Child = content;
                
                // Update title with app's title
                Title = $"WPF Host - {window.Title}";
            }
            
            _loadCount++;
            LoadCountText.Text = $"Loads: {_loadCount}";
            StatusText.Text = $" - Loaded ({DateTime.Now:HH:mm:ss})";
            
            Console.WriteLine($"[Host] App loaded successfully (load #{_loadCount})");
            
            // Clean up the temp file after a delay
            Task.Delay(5000).ContinueWith(_ => 
            {
                try { File.Delete(tempPath); } catch { }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Host] Failed to load app: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            StatusText.Text = " - Load failed!";
            ShowMessage($"Load failed: {ex.Message}");
        }
    }
    
    private void UnloadApp()
    {
        if (_currentContext != null)
        {
            Console.WriteLine("[Host] Unloading previous app...");
            AppContainer.Child = null;
            
            _currentContext.Unload();
            _currentContext = null;
            
            // Encourage garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
    
    private void ShowMessage(string message)
    {
        AppContainer.Child = new TextBlock
        {
            Text = message,
            Foreground = System.Windows.Media.Brushes.Orange,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 18,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20)
        };
    }
    
    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        Console.WriteLine("[Host] Manual reload requested");
        LoadApp();
    }
    
    private void UnloadButton_Click(object sender, RoutedEventArgs e)
    {
        Console.WriteLine("[Host] Manual unload requested");
        UnloadApp();
        StatusText.Text = " - Unloaded";
        ShowMessage("App unloaded. Click Reload to load again.");
    }
    
    protected override void OnClosed(EventArgs e)
    {
        _watcher?.Dispose();
        UnloadApp();
        base.OnClosed(e);
    }
}

/// <summary>
/// Custom AssemblyLoadContext that can be unloaded
/// </summary>
public class AppLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    
    public AppLoadContext(string mainAssemblyPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
    }
    
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Let WPF and system assemblies load from default context
        if (assemblyName.Name?.StartsWith("System") == true ||
            assemblyName.Name?.StartsWith("Microsoft") == true ||
            assemblyName.Name?.StartsWith("Windows") == true ||
            assemblyName.Name?.StartsWith("Presentation") == true)
        {
            return null;
        }
        
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }
        
        return null;
    }
}
