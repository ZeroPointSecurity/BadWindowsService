using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;

namespace BadWindowsService;

public partial class BadWindowsService : ServiceBase
{
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr LoadLibraryA(string lpFileName);
    
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetModuleHandleA(string lpModuleName);

    private CancellationTokenSource _cts;

    public BadWindowsService()
    {
        InitializeComponent();
    }

    protected override void OnStart(string[] args)
    {
        // add C:\Windows\Temp to the start of the PATH environment variable
        var currentPath = Environment.GetEnvironmentVariable("PATH");
        var newPath = $@"C:\Windows\Temp\;{currentPath}";
        Environment.SetEnvironmentVariable("PATH", newPath);
        
        // set working directory to C:\Temp
        Directory.SetCurrentDirectory(@"C:\Temp");

        _cts = new CancellationTokenSource();
        
        // run the loop
        var t = new Thread(DoBadThings);
        t.Start();
    }

    protected override void OnStop()
    {
        _cts.Cancel();
    }

    private void DoBadThings()
    {
        const string moduleName = "BadDll.dll";
        
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                // load module
                if (GetModuleHandleA(moduleName) == IntPtr.Zero)
                    _ = LoadLibraryA(moduleName);
            }
            catch { };

            try
            {
                // run executable
                Process.Start("cmd.exe", "/c exit");
            }
            catch { };

            Thread.Sleep(new TimeSpan(0, 1, 0));
        }
        
        _cts.Dispose();
    }
}