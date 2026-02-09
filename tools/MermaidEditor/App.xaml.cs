using System.Configuration;
using System.Data;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;

namespace MermaidEditor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private const string MutexName = "MermaidEditor_SingleInstance_Mutex";
    private const string PipeName = "MermaidEditor_SingleInstance_Pipe";
    private static Mutex? _mutex;
    private static bool _ownsMutex = false;
    private CancellationTokenSource? _pipeServerCts;

    protected override void OnStartup(StartupEventArgs e)
    {
        bool createdNew;
        _mutex = new Mutex(true, MutexName, out createdNew);
        _ownsMutex = createdNew;

        if (!createdNew)
        {
            // Another instance is already running - send file path via named pipe
            if (e.Args.Length > 0 && File.Exists(e.Args[0]))
            {
                SendFilePathToExistingInstance(e.Args[0]);
            }
            else
            {
                // Just bring existing instance to foreground
                SendFilePathToExistingInstance("");
            }
            
            Shutdown();
            return;
        }

        // Start listening for file paths from other instances
        StartPipeServer();
        
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pipeServerCts?.Cancel();
        if (_ownsMutex && _mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // Ignore errors releasing mutex
            }
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private void SendFilePathToExistingInstance(string filePath)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1000); // 1 second timeout
            
            using var writer = new StreamWriter(client);
            writer.WriteLine(filePath);
            writer.Flush();
        }
        catch
        {
            // If we can't connect, just let the app start normally
        }
    }

    private void StartPipeServer()
    {
        _pipeServerCts = new CancellationTokenSource();
        var token = _pipeServerCts.Token;
        
        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(token);
                    
                    using var reader = new StreamReader(server);
                    var filePath = await reader.ReadLineAsync();
                    
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        // Open file in existing window on UI thread
                        Dispatcher.Invoke(() =>
                        {
                            if (MainWindow is MainWindow mainWindow)
                            {
                                mainWindow.OpenFileFromExternalSource(filePath);
                                mainWindow.Activate();
                                if (mainWindow.WindowState == WindowState.Minimized)
                                {
                                    mainWindow.WindowState = WindowState.Normal;
                                }
                            }
                        });
                    }
                    else
                    {
                        // Just bring window to foreground
                        Dispatcher.Invoke(() =>
                        {
                            if (MainWindow is MainWindow mainWindow)
                            {
                                mainWindow.Activate();
                                if (mainWindow.WindowState == WindowState.Minimized)
                                {
                                    mainWindow.WindowState = WindowState.Normal;
                                }
                            }
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Continue listening even if there's an error
                }
            }
        }, token);
    }
}

