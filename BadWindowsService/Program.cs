using System.ServiceProcess;

namespace BadWindowsService;

internal static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    public static void Main()
    {
        ServiceBase[] services = [ new BadWindowsService() ];
        ServiceBase.Run(services);
    }
}