using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;

namespace BadWindowsService;

public partial class BadWindowsService : ServiceBase
{
    [DllImport("kernel32", CharSet = CharSet.Ansi)]
    private static extern IntPtr LoadLibraryA(string lpFileName);

    [DllImport("kernel32", CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool BadFunc();

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
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                const string moduleName = "BadDll.dll";
                const string funcName = "BadFunc";
                
                // load module
                var hModule = LoadLibraryA(moduleName);
                if (hModule == IntPtr.Zero)
                    throw new DllNotFoundException($"{moduleName} not found.");
                
                // get func address
                var hFunc = GetProcAddress(hModule, funcName);
                if (hFunc == IntPtr.Zero)
                    throw new ApplicationException($"{funcName} not found.");
                
                // marshal function pointer
                var badFunc = Marshal.GetDelegateForFunctionPointer<BadFunc>(hFunc);
                
                // execute it
                if (badFunc() == false)
                    throw new ApplicationException("Result from BadFunc was false.");
            }
            catch { };

            try
            {
                // run executable
                Process.Start("cmd.exe", "/c exit");
            }
            catch { };

            Thread.Sleep(new TimeSpan(0, 0, 30));
        }
        
        _cts.Dispose();
    }
}