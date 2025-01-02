using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;

namespace BadWindowsService;

public partial class BadWindowsService : ServiceBase
{
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
        
        // run the loops
        var t1 = new Thread(LoadDll);
        var t2 = new Thread(RunExecutable);
        var t3 = new Thread(DeserializeFile);
        var t4 = new Thread(Race);
        
        t1.Start();
        t2.Start();
        t3.Start();
        t4.Start();
    }

    protected override void OnStop()
    {
        _cts.Cancel();
    }

    private void LoadDll()
    {
        const string moduleName = "BadDll.dll";
        const string funcName = "BadFunc";
        
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                // load module
                var hModule = Win32.LoadLibraryA(moduleName);
                if (hModule == IntPtr.Zero)
                    throw new DllNotFoundException($"{moduleName} not found.");

                // get func address
                var hFunc = Win32.GetProcAddress(hModule, funcName);
                if (hFunc == IntPtr.Zero)
                    throw new ApplicationException($"{funcName} not found.");

                // marshal function pointer
                var badFunc = Marshal.GetDelegateForFunctionPointer<BadFunc>(hFunc);

                // execute it
                if (badFunc() == false)
                    throw new ApplicationException("Result from BadFunc was false.");
            }
            catch
            {
                // ignore
            }

            Thread.Sleep(new TimeSpan(0, 0, 30));
        }
    }

    private void RunExecutable()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                Process.Start("cmd.exe", "/c exit");
            }
            catch
            {
                // ignore
            }
            
            Thread.Sleep(new TimeSpan(0, 1, 0));
        }
    }

    private void DeserializeFile()
    {
        const string file = "data.bin";
        
        while (!_cts.IsCancellationRequested)
        {
            if (File.Exists(file))
            {
                try
                {
                    var fs = File.OpenRead(file);
                    var bf = new BinaryFormatter();
                    _ = bf.UnsafeDeserialize(fs, null);
                }
                catch
                {
                    // ignore
                }
            }
            
            Thread.Sleep(new TimeSpan(0, 0, 30));
        }
    }

    private void Race()
    {
        const string file = "data.bin";
        
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (!File.Exists(file))
                {
                    // create it
                    using var fs = File.Create(file);
                    
                    // simulate some other work
                    Thread.Sleep(new TimeSpan(0, 0, 0, 0, 500));
                    
                    // set new permissions on file
                    var rule = new FileSystemAccessRule(
                        new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                        FileSystemRights.FullControl, 
                        AccessControlType.Allow);
                    
                    var security = new FileSecurity();
                    security.AddAccessRule(rule);
                    File.SetAccessControl(file, security);
                }
            }
            catch
            {
                // ignore
            }
            
            Thread.Sleep(new TimeSpan(0, 0, 30));
        }
    }
}