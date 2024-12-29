using System.ComponentModel;
using System.Configuration.Install;

namespace BadWindowsService;

[RunInstaller(true)]
public partial class ProjectInstaller : Installer
{
    public ProjectInstaller()
    {
        InitializeComponent();
    }
}