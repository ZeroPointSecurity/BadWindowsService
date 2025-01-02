using System;
using System.Runtime.InteropServices;

namespace BadWindowsService;

public static class Win32
{
    [DllImport("kernel32", CharSet = CharSet.Ansi)]
    public static extern IntPtr LoadLibraryA(string lpFileName);

    [DllImport("kernel32", CharSet = CharSet.Ansi)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
}