using System;
using System.Security.AccessControl;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;

namespace Installer;

internal static class Program
{
    private static bool IsElevated()
    {
        return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void RunCommandWriteOutput(string program, string arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = program;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.Start();

        // Synchronously read the standard output of the spawned process.
        var reader = process.StandardOutput;
        var output = reader.ReadToEnd();

        // Write the redirected output to this application's window.
        Console.WriteLine(output);

        process.WaitForExit();
    }

    private static void CreateDirectoryWithFullControl(string path)
    {
        var rule = new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), 
            FileSystemRights.FullControl, 
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, 
            AccessControlType.Allow);
        
        var security = new DirectorySecurity();
        security.AddAccessRule(rule);

        Directory.CreateDirectory(path, security);
    }

    public static void Main(string[] args)
    {
        // Verify elevation
        if (!IsElevated())
        {
            Console.Error.WriteLine("[X] The installer must be launched in an elevated context");
            return;
        }

        const string parentDirectory = @"C:\Program Files\Bad Windows Service";
        const string childDirectory = $@"{parentDirectory}\Service Executable";
        const string executable = "BadWindowsService.exe";
        const string dll = "BadDll.dll";
        const string exePath = $@"{childDirectory}\{executable}";
        const string dllPath = $@"{parentDirectory}\{dll}";
        const string svcName = "BadWindowsService";
        const string temp = @"C:\Temp";

        // create parent directory
        Directory.CreateDirectory(parentDirectory);
        Console.WriteLine("[+] Created folder {0}", parentDirectory);
        
        // create service directory with full control
        CreateDirectoryWithFullControl(childDirectory);
        Console.WriteLine("[+] Created folder {0}", childDirectory);

        // copy executable to destination
        if (File.Exists(executable))
        {
            File.Copy(executable, exePath, true);
            Console.WriteLine("[+] Copied {0} to {1}", executable, exePath);

            // grant full control
            var rule = new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                FileSystemRights.FullControl, 
                AccessControlType.Allow);
            
            var security = new FileSecurity();
            security.AddAccessRule(rule);
            File.SetAccessControl(exePath, security);
            Console.WriteLine("[+] Granted AuthenticatedUserSid Full Control");
        }
        else
        {
            Console.Error.WriteLine("[X] Service executable not found in current working directory");
            return;
        }
        
        // copy DLL to destination
        if (File.Exists(dll))
        {
            File.Copy(dll, dllPath, true);
            Console.WriteLine("[+] Copied {0} to {1}", dll, dllPath);
        }
        else
        {
            Console.Error.WriteLine("[X] Dll not found in current working directory");
            return;
        }

        // Run installer
        var installUtilPath = RuntimeEnvironment.GetRuntimeDirectory() + "InstallUtil.exe";
        
        if (!File.Exists(installUtilPath))
        {
            Console.Error.WriteLine("[X] Could not locate InstallUtil.exe");
            return;
        }
        
        try
        {
            RunCommandWriteOutput(installUtilPath, $"\"{exePath}\"");
            Console.WriteLine("[+] Service installed");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[X] Service installation failed: {0}", ex.Message);
            return;
        }

        try
        {
            // modify service binpath to be an unquoted path
            RunCommandWriteOutput("sc.exe", $"config {svcName} binpath= \"{exePath}\"");
            Console.WriteLine("[+] Service binpath is now unquoted");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[!] Service binpath modification failed: {0}", ex.Message);
            return;
        }

        try
        {
            // modify service permissions
            RunCommandWriteOutput("sc.exe", $"sdset {svcName} \"D:PAI(A;;FA;;;AU)\"");
            Console.WriteLine("[+] Granted AuthenticatedUserSid control on the service");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[!] Service permissions modification failed: {0}", ex.Message);
            return;
        }

        try
        {
            // grant full control over the service's registry key
            var rs = new RegistrySecurity();
            
            rs.AddAccessRule(new RegistryAccessRule(
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                RegistryRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            
            var rk = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{svcName}", true);
            rk.SetAccessControl(rs);
            
            Console.WriteLine("[+] Granted AuthenticatedUserSid Full Control on the service's Registry key");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[!] Service registry permissions modification failed: {0}", ex.Message);
            return;
        }
        
        // create C:\TEMP
        if (!Directory.Exists(temp)) 
            Directory.CreateDirectory(temp);
        
        // add directory to path variable
        var path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
        Environment.SetEnvironmentVariable("PATH", $"{path};{parentDirectory}", EnvironmentVariableTarget.Machine);
        Console.WriteLine($"[+] Added {parentDirectory} to machine PATH variable");

        // start the service
        var service = new ServiceController(svcName);
        service.Start();
        Thread.Sleep(3000);

        if (service.Status == ServiceControllerStatus.Running)
        {
            Console.WriteLine("[+] Service started!");
        }
        else
        {
            Console.Error.WriteLine("[X] Service failed to start");
        } 
    }
}