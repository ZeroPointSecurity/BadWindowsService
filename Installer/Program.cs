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

    private static bool CreateDirectWithFullControl(string path)
    {
        var directoryRule = new FileSystemAccessRule(
            "Everyone", 
            FileSystemRights.FullControl, 
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, 
            AccessControlType.Allow);
        
        var directorySecurity = new DirectorySecurity();
        directorySecurity.AddAccessRule(directoryRule);

        if (Directory.Exists(path))
            return false;

        Directory.CreateDirectory(path, directorySecurity);
        return true;
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
        const string fullPath = $@"{childDirectory}\{executable}";
        const string svcName = "BadWindowsService";
        const string temp = @"C:\Temp";

        // Create folder structure and grant Everyone full control
        if (CreateDirectWithFullControl(parentDirectory))
        {
            Console.WriteLine("[+] Created folder {0}", parentDirectory);
        }
        else
        {
            Console.WriteLine("[*] Folder {0} already exists", parentDirectory);
        }
        
        if (CreateDirectWithFullControl(childDirectory))
        {
            Console.WriteLine("[+] Created folder {0}", childDirectory);
        }
        else
        {
            Console.WriteLine("[*] Folder {0} already exists", childDirectory);
        }

        // Copy executable to destination
        if (File.Exists(executable))
        {
            Console.WriteLine("[+] Located {0} in current working directory",executable);
            File.Copy(executable, fullPath, true);
            Console.WriteLine(@"[+] Copied {0} to {1}", executable, fullPath);

            // Grant Everyone full control
            var fileRule = new FileSystemAccessRule(
                "Everyone",
                FileSystemRights.FullControl, 
                AccessControlType.Allow);
            
            var fileSecurity = new FileSecurity();
            fileSecurity.AddAccessRule(fileRule);
            File.SetAccessControl(fullPath, fileSecurity);
            Console.WriteLine("[+] Granted Everyone full control");
        }
        else
        {
            Console.Error.WriteLine("[X] Service executable not found in current working directory");
        }

        //Run installer
        var installUtilPath = RuntimeEnvironment.GetRuntimeDirectory() + "InstallUtil.exe";
        
        if (!File.Exists(installUtilPath))
        {
            Console.Error.WriteLine("[X] Could not locate InstallUtil.exe");
            return;
        }
        
        try
        {
            RunCommandWriteOutput(installUtilPath, $"\"{fullPath}\"");
            Console.WriteLine("[+] Service installed");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[X] Service installation failed: {0}", ex.Message);
            return;
        }

        // Modify service binpath to be an unquoted path
        try
        {
            RunCommandWriteOutput("sc.exe", $"config {svcName} binpath= \"{fullPath}\"");
            Console.WriteLine("[+] Service binpath is now unquoted");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[!] Service binpath modification failed: {0}", ex.Message);
            return;
        }

        // Modify service permissions - grant Everyone full control
        try
        {
            RunCommandWriteOutput("sc.exe", $"sdset {svcName} \"D:PAI(A;;FA;;;WD)\"");
            Console.WriteLine("[+] Granted Everyone full control on the service");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[!] Service permissions modification failed: {0}", ex.Message);
            return;
        }

        // Grant Everyone full control over the service's Registry key
        try
        {
            var rs = new RegistrySecurity();
            rs.AddAccessRule(new RegistryAccessRule(
                "Everyone",
                RegistryRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            var rk = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{svcName}", true);
            rk.SetAccessControl(rs);
            Console.WriteLine("[+] Granted Everyone full control on the service's Registry key");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[!] Service registry permissions modification failed: {0}", ex.Message);
            return;
        }
        
        // create C:\TEMP
        if (!Directory.Exists(temp)) 
            Directory.CreateDirectory(temp);

        // Start the service
        var service = new ServiceController(svcName);
        service.Start();
        Thread.Sleep(3000);
        
        if (service.Status == ServiceControllerStatus.Running)
            Console.WriteLine("[+] Service started!"); 
        else
            Console.Error.WriteLine("[X] Service failed to start"); 
    }
}