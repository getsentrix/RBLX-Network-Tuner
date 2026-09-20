using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Roblox Network Tuner Setup")]
[assembly: AssemblyDescription("Roblox Network Tuner Standalone Installer and Setup Bootstrapper")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyCompany("Roblox Performance Engineering")]
[assembly: AssemblyProduct("Roblox Network Tuner")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("3a4d6201-9f14-4e86-8e30-22d29cda328c")]
[assembly: AssemblyVersion("2.4.0.0")]
[assembly: AssemblyFileVersion("2.4.0.0")]

namespace RobloxNetworkTuner.Setup
{
    public delegate void ProgressReportHandler(float progress, int milestoneIndex, string statusText);

    internal static class Program
    {
        public const string AppTitle = "Roblox Network Tuner";
        public const string AppVersion = "2.4.0";
        public const string PublisherName = "Roblox Performance Engineering";
        public const string UninstallRegSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RobloxNetworkTuner";
        public const string QosPolicyName = "RobloxNetworkTuner_DSCP46";

        #region Native Methods

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtSetTimerResolution(uint desiredResolution, bool setResolution, out uint currentResolution);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
        public static extern uint TimeEndPeriod(uint uMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(int dwProcessId);
        public const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int nStdHandle);

        public static void EnsureConsoleAttached()
        {
            try
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                IntPtr stdOut = GetStdHandle(-11);
                if (stdOut != IntPtr.Zero && stdOut != new IntPtr(-1))
                {
                    Microsoft.Win32.SafeHandles.SafeFileHandle sfh = new Microsoft.Win32.SafeHandles.SafeFileHandle(stdOut, false);
                    FileStream fs = new FileStream(sfh, FileAccess.Write);
                    StreamWriter writer = new StreamWriter(fs, System.Text.Encoding.Default);
                    writer.AutoFlush = true;
                    Console.SetOut(writer);
                }
            }
            catch { }
        }

        #endregion

        [STAThread]
        private static int Main(string[] args)
        {
            bool isSilent = false;
            bool isUninstall = false;
            bool isWorker = false;
            string workerInstallDir = null;
            int parentPid = 0;
            string customDir = null;

            string myExeName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            if (myExeName.Equals("uninstall.exe", StringComparison.OrdinalIgnoreCase))
            {
                isUninstall = true;
            }

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i].ToLowerInvariant();
                    if (a == "--help" || a == "-h" || a == "/?" || a == "-help")
                    {
                        EnsureConsoleAttached();
                        PrintHelp();
                        return 0;
                    }
                    if (a == "--uninstall" || a == "-u" || a == "/u" || a == "/uninstall")
                    {
                        isUninstall = true;
                    }
                    else if (a == "--silent" || a == "-s" || a == "/s" || a == "/silent")
                    {
                        isSilent = true;
                    }
                    else if ((a == "--dir" || a == "-d") && i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        customDir = args[++i];
                    }
                    else if (a == "--uninstall-worker" && i + 2 < args.Length)
                    {
                        isWorker = true;
                        workerInstallDir = args[++i];
                        int.TryParse(args[++i], out parentPid);
                    }
                }
            }

            // Check Administrator privileges
            bool isAdmin = IsAdministrator();
            if (!isAdmin && !isSilent)
            {
                if (ElevateProcess(args))
                {
                    return 0;
                }
            }

            // Worker mode handles uninstallation from %TEMP%
            if (isWorker)
            {
                if (isSilent)
                {
                    return RunUninstallWorkerSilent(workerInstallDir, parentPid);
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SetupGuiForm(workerInstallDir, true, parentPid));
                return 0;
            }

            if (isUninstall)
            {
                return DispatchUninstall(isSilent);
            }

            // Interactive or Silent Installation
            string targetDir = customDir;
            if (string.IsNullOrEmpty(targetDir))
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (string.IsNullOrEmpty(progFiles)) progFiles = @"C:\Program Files";
                targetDir = Path.Combine(progFiles, "RobloxNetworkTuner");
            }

            if (isSilent)
            {
                RunInstallWork(targetDir, null);
                return 0;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupGuiForm(targetDir, false, 0));
            return 0;
        }

        #region Elevation & Environment

        public static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool ElevateProcess(string[] args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Process.GetCurrentProcess().MainModule.FileName;
                if (args != null && args.Length > 0)
                {
                    psi.Arguments = string.Join(" ", args);
                }
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                Process proc = Process.Start(psi);
                return proc != null;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Core Installation Engine

        public static void RunInstallWork(string targetDir, ProgressReportHandler report)
        {
            byte[] tunerBytes = null;
            string tunerExePath = Path.Combine(targetDir, "RobloxNetworkTuner.exe");
            string restoreBatPath = Path.Combine(targetDir, "Restore-Stock.bat");
            string readmeTxtPath = Path.Combine(targetDir, "README.txt");
            string uninstallerPath = Path.Combine(targetDir, "uninstall.exe");

            // Phase 0: System Verification
            if (report != null) report(0.05f, 0, "Checking Windows kernel architecture & privileges...");
            Thread.Sleep(60);
            if (!Environment.Is64BitOperatingSystem)
            {
                throw new PlatformNotSupportedException("64-bit Windows required.");
            }

            if (report != null) report(0.12f, 0, "Initializing secure directory structure...");
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            DirectoryInfo di = new DirectoryInfo(targetDir);
            di.Attributes = FileAttributes.Normal;
            Thread.Sleep(50);

            // Phase 1: Engine Extraction
            if (report != null) report(0.22f, 1, "Unpacking installation files...");
            tunerBytes = ExtractOrDecompressPayload("RobloxNetworkTuner.pkg", "RobloxNetworkTuner.exe");
            if (tunerBytes == null || tunerBytes.Length == 0)
            {
                throw new FileNotFoundException("Engine payload could not be loaded.");
            }
            Thread.Sleep(60);

            if (report != null) report(0.32f, 1, string.Format("Extracting RobloxNetworkTuner.exe ({0:N0} bytes)...", tunerBytes.Length));
            Thread.Sleep(50);

            if (report != null) report(0.42f, 1, "Installing RobloxNetworkTuner.exe...");
            File.WriteAllBytes(tunerExePath, tunerBytes);
            Thread.Sleep(60);

            if (report != null) report(0.48f, 1, "Creating Restore-Defaults.bat...");
            string batContent =
                "@echo off\r\n" +
                "setlocal EnableDelayedExpansion\r\n" +
                "title Restore Default Network Settings\r\n" +
                "cd /d \"%~dp0\"\r\n" +
                "if exist \"%~dp0RobloxNetworkTuner.exe\" (\r\n" +
                "    \"%~dp0RobloxNetworkTuner.exe\" --restore\r\n" +
                ")\r\n" +
                "echo.\r\n" +
                "echo Default network settings have been restored.\r\n" +
                "pause\r\n";
            File.WriteAllText(restoreBatPath, batContent);
            Thread.Sleep(40);

            if (report != null) report(0.54f, 1, "Generating reference guide and uninstaller...");
            string readmeContent =
                "================================================================================\r\n" +
                "ROBLOX NETWORK TUNER [x64] - QUICK START GUIDE\r\n" +
                "================================================================================\r\n\r\n" +
                "Roblox Network Tuner is a dedicated low-latency and anti-jitter optimization\r\n" +
                "tool engineered for Roblox on Windows 10 and Windows 11.\r\n\r\n" +
                "FEATURES:\r\n" +
                "- AFD Fast-Path: Locks Winsock datagram buffers to bypass socket queuing delays.\r\n" +
                "- Global 0.50 ms Timer: Sets NT kernel hardware interrupt timer to 0.50 ms (2000 Hz).\r\n" +
                "- Priority & Power Boost: Runs RobloxPlayerBeta.exe with HIGH_PRIORITY_CLASS,\r\n" +
                "  High I/O priority, and explicitly bypasses Windows 11 EcoQoS / Power Throttling.\r\n" +
                "- QoS DSCP 46 Tagging: Tags outgoing packets with Expedited Forwarding priority.\r\n" +
                "- Wi-Fi Scan Freeze: Locks WLAN radio to prevent 60-second background scan spikes.\r\n" +
                "- Nagle Disablement: Eliminates delayed ACKs and packet coalescing.\r\n" +
                "- Automated Jitter Benchmark: Measures packet pacing and jitter variance (RFC 3550).\r\n\r\n" +
                "HOW TO USE:\r\n" +
                "1. Launch 'Roblox Network Tuner' from your Start Menu or Desktop.\r\n" +
                "2. Accept the Windows UAC elevation prompt (Administrator privileges required).\r\n" +
                "3. The engine activates all optimizations and waits for RobloxPlayerBeta.exe.\r\n" +
                "4. When you finish playing, press [Space], [Q], [Esc], or simply close Roblox.\r\n" +
                "   Settings automatically revert to Windows defaults when Roblox closes.\r\n\r\n" +
                "UNINSTALLATION:\r\n" +
                "Uninstall via Windows Settings -> Apps -> Installed Apps -> Roblox Network Tuner.\r\n" +
                "================================================================================\r\n";
            File.WriteAllText(readmeTxtPath, readmeContent);

            string currentExe = Process.GetCurrentProcess().MainModule.FileName;
            if (!string.Equals(currentExe, uninstallerPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(currentExe, uninstallerPath, true);
            }
            Thread.Sleep(50);

            // Phase 2: System Integration
            if (report != null) report(0.62f, 2, "Creating Start Menu shortcuts...");
            string progDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            if (string.IsNullOrEmpty(progDir) || !Directory.Exists(progDir))
            {
                progDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            }
            string groupDir = Path.Combine(progDir, "Roblox Network Tuner");
            if (!Directory.Exists(groupDir)) Directory.CreateDirectory(groupDir);

            CreateShortcut(Path.Combine(groupDir, "Roblox Network Tuner.lnk"), tunerExePath, targetDir, "", "Roblox Network Tuner", tunerExePath + ",0");
            CreateShortcut(Path.Combine(groupDir, "Reset Network Settings.lnk"), tunerExePath, targetDir, "--restore", "Restore Default Windows Network Settings", tunerExePath + ",0");
            CreateShortcut(Path.Combine(groupDir, "Uninstall Roblox Network Tuner.lnk"), uninstallerPath, targetDir, "--uninstall", "Uninstall Roblox Network Tuner", uninstallerPath + ",0");
            Thread.Sleep(60);

            if (report != null) report(0.72f, 2, "Creating Desktop shortcut...");
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string lnk = Path.Combine(desktopPath, "Roblox Network Tuner.lnk");
            CreateShortcut(lnk, tunerExePath, targetDir, "", "Roblox Network Tuner", tunerExePath + ",0");

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] extraDesktops = new string[]
            {
                Path.Combine(userProfile, @"OneDrive\Desktop"),
                Path.Combine(userProfile, @"OneDrive\Everything\Desktop"),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };
            for (int i = 0; i < extraDesktops.Length; i++)
            {
                if (Directory.Exists(extraDesktops[i]))
                {
                    string extraLnk = Path.Combine(extraDesktops[i], "Roblox Network Tuner.lnk");
                    CreateShortcut(extraLnk, tunerExePath, targetDir, "", "Roblox Low-Latency & Anti-Jitter Tuner", tunerExePath + ",0");
                }
            }
            Thread.Sleep(50);

            // Phase 3: Finalizing
            if (report != null) report(0.85f, 3, "Registering in Windows Add/Remove Programs (Registry)...");
            RegistryKey baseKey = IsAdministrator() ? Registry.LocalMachine : Registry.CurrentUser;
            using (RegistryKey key = baseKey.CreateSubKey(UninstallRegSubKey))
            {
                if (key != null)
                {
                    key.SetValue("DisplayName", AppTitle, RegistryValueKind.String);
                    key.SetValue("DisplayVersion", AppVersion, RegistryValueKind.String);
                    key.SetValue("Publisher", PublisherName, RegistryValueKind.String);
                    key.SetValue("InstallLocation", targetDir, RegistryValueKind.String);
                    key.SetValue("UninstallString", "\"" + uninstallerPath + "\" --uninstall", RegistryValueKind.String);
                    key.SetValue("QuietUninstallString", "\"" + uninstallerPath + "\" --uninstall --silent", RegistryValueKind.String);
                    key.SetValue("DisplayIcon", tunerExePath + ",0", RegistryValueKind.String);
                    key.SetValue("EstimatedSize", (tunerBytes != null ? tunerBytes.Length / 1024 : 100) + 150, RegistryValueKind.DWord);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            Thread.Sleep(60);

            if (report != null) report(0.95f, 3, "Flushing file buffers & committing installation...");
            Thread.Sleep(100);

            if (report != null) report(1.0f, 3, "Installation completed successfully!");
        }

        #endregion

        #region Core Uninstallation Engine

        public static void RunUninstallWork(string installDir, ProgressReportHandler report)
        {
            // Phase 0: Process Termination
            if (report != null) report(0.10f, 0, "Closing running processes...");
            Process[] procs = Process.GetProcessesByName("RobloxNetworkTuner");
            for (int i = 0; i < procs.Length; i++)
            {
                try
                {
                    procs[i].Kill();
                    procs[i].WaitForExit(3000);
                }
                catch { }
            }
            Thread.Sleep(60);

            // Phase 1: Kernel & Timer Settings
            if (report != null) report(0.25f, 1, "Resetting kernel timer resolution...");
            uint dummy;
            NtSetTimerResolution(156250, false, out dummy);
            TimeEndPeriod(1);
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", true))
            {
                if (k != null) k.DeleteValue("GlobalTimerResolutionRequests", false);
            }
            Thread.Sleep(50);

            if (report != null) report(0.35f, 1, "Enabling Wi-Fi autoconfig scanning...");
            string wifiOut = RunCapture("netsh.exe", "wlan show interfaces");
            MatchCollection matches = Regex.Matches(wifiOut, @"^\s*Name\s*:\s*(.+)$", RegexOptions.Multiline);
            foreach (Match m in matches)
            {
                string nicName = m.Groups[1].Value.Trim();
                RunSilent("netsh.exe", string.Format("wlan set autoconfig enabled=yes interface=\"{0}\"", nicName));
            }
            Thread.Sleep(50);

            if (report != null) report(0.45f, 1, "Removing QoS DSCP policy...");
            try
            {
                using (RegistryKey polKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\QoS", true))
                {
                    if (polKey != null) polKey.DeleteSubKeyTree(QosPolicyName, false);
                }
            }
            catch { }
            RunSilent("powershell.exe", string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"Remove-NetQosPolicy -Name '{0}' -Confirm:$false -ErrorAction SilentlyContinue\"", QosPolicyName));
            Thread.Sleep(50);

            // Phase 2: Network Stack Reset
            if (report != null) report(0.55f, 2, "Resetting Winsock AFD parameters...");
            using (RegistryKey afdKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\AFD\Parameters", true))
            {
                if (afdKey != null)
                {
                    afdKey.DeleteValue("FastSendDatagramThreshold", false);
                    afdKey.DeleteValue("FastCopyReceiveThreshold", false);
                    afdKey.DeleteValue("DefaultReceiveWindow", false);
                    afdKey.DeleteValue("DefaultSendWindow", false);
                    afdKey.DeleteValue("DoNotDisableReceiveBuffering", false);
                    afdKey.DeleteValue("DoNotDisableSendBuffering", false);
                    afdKey.DeleteValue("NonBlockingSendLimits", false);
                }
            }
            Thread.Sleep(40);

            if (report != null) report(0.65f, 2, "Resetting TCP/IP stack parameters...");
            using (RegistryKey tcpKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", true))
            {
                if (tcpKey != null)
                {
                    tcpKey.DeleteValue("EnablePMTUDiscovery", false);
                    tcpKey.DeleteValue("EnablePMTUBHDetect", false);
                    tcpKey.DeleteValue("DefaultTTL", false);
                    tcpKey.DeleteValue("DisableTaskOffload", false);
                    tcpKey.DeleteValue("EnableDCA", false);
                    tcpKey.DeleteValue("MaxUserPort", false);
                    tcpKey.DeleteValue("TcpTimedWaitDelay", false);
                }
            }
            Thread.Sleep(40);

            if (report != null) report(0.72f, 2, "Resetting TCP ACK frequency...");
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface nic in interfaces)
            {
                if (nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    string path = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + nic.Id;
                    using (RegistryKey nicKey = Registry.LocalMachine.OpenSubKey(path, true))
                    {
                        if (nicKey != null)
                        {
                            nicKey.DeleteValue("TcpAckFrequency", false);
                            nicKey.DeleteValue("TCPNoDelay", false);
                            nicKey.DeleteValue("TcpDelAckTicks", false);
                        }
                    }
                }
            }
            Thread.Sleep(40);

            if (report != null) report(0.78f, 2, "Resetting congestion provider & MMCSS...");
            RunSilent("netsh.exe", "int tcp set global rsc=enabled");
            RunSilent("netsh.exe", "int tcp set global timestamps=allowed");
            RunSilent("netsh.exe", "int tcp set supplemental template=internet congestionprovider=default");
            RunSilent("netsh.exe", "int tcp set supplemental template=compat congestionprovider=default");
            RunSilent("netsh.exe", "int tcp set supplemental template=datacenterext congestionprovider=default");
            RunSilent("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"Set-NetOffloadGlobalSetting -PacketCoalescingFilter Enabled -ReceiveSegmentCoalescing Enabled -Confirm:$false\"");

            using (RegistryKey mmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", true))
            {
                if (mmKey != null)
                {
                    mmKey.SetValue("NetworkThrottlingIndex", 10, RegistryValueKind.DWord);
                    mmKey.SetValue("SystemResponsiveness", 20, RegistryValueKind.DWord);
                    mmKey.DeleteValue("NoLazyMode", false);
                    mmKey.DeleteValue("AlwaysOn", false);
                }
            }
            RunSilent("ipconfig.exe", "/flushdns");
            RunSilent("netsh.exe", "interface ip delete arpcache");
            Thread.Sleep(50);

            // Phase 3: System Cleanup
            if (report != null) report(0.85f, 3, "Removing shortcuts...");
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] desktopPaths = new string[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Path.Combine(userProfile, @"OneDrive\Desktop"),
                Path.Combine(userProfile, @"OneDrive\Everything\Desktop"),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };
            for (int i = 0; i < desktopPaths.Length; i++)
            {
                if (!string.IsNullOrEmpty(desktopPaths[i]) && Directory.Exists(desktopPaths[i]))
                {
                    string l = Path.Combine(desktopPaths[i], "Roblox Network Tuner.lnk");
                    if (File.Exists(l)) { try { File.Delete(l); } catch { } }
                }
            }

            string[] startMenuPaths = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Roblox Network Tuner"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Roblox Network Tuner")
            };
            for (int i = 0; i < startMenuPaths.Length; i++)
            {
                if (Directory.Exists(startMenuPaths[i]))
                {
                    try { Directory.Delete(startMenuPaths[i], true); } catch { }
                }
            }

            try { Registry.LocalMachine.DeleteSubKeyTree(UninstallRegSubKey, false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallRegSubKey, false); } catch { }
            Thread.Sleep(50);

            if (report != null) report(0.95f, 3, "Removing application files...");
            if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
            {
                string dirName = Path.GetFileName(installDir.TrimEnd('\\', '/'));
                bool isSafe = dirName.Equals("RobloxNetworkTuner", StringComparison.OrdinalIgnoreCase) ||
                             dirName.StartsWith("RNT_", StringComparison.OrdinalIgnoreCase) ||
                             dirName.IndexOf("Roblox", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             File.Exists(Path.Combine(installDir, "RobloxNetworkTuner.exe"));

                if (isSafe && !Path.GetPathRoot(installDir).Equals(Path.GetFullPath(installDir), StringComparison.OrdinalIgnoreCase))
                {
                    string[] files = Directory.GetFiles(installDir);
                    for (int i = 0; i < files.Length; i++)
                    {
                        try { File.SetAttributes(files[i], FileAttributes.Normal); File.Delete(files[i]); } catch { }
                    }
                    string[] subdirs = Directory.GetDirectories(installDir);
                    for (int i = 0; i < subdirs.Length; i++)
                    {
                        try { Directory.Delete(subdirs[i], true); } catch { }
                    }
                    for (int r = 0; r < 5; r++)
                    {
                        try { Directory.Delete(installDir, true); break; } catch { Thread.Sleep(200); }
                    }
                }
            }
            Thread.Sleep(50);

            if (report != null) report(1.0f, 3, "Uninstallation complete. Default settings restored.");
        }

        private static int DispatchUninstall(bool isSilent)
        {
            string currentExe = Process.GetCurrentProcess().MainModule.FileName;
            string currentDir = Path.GetDirectoryName(currentExe);
            string installDir = null;

            if (File.Exists(Path.Combine(currentDir, "RobloxNetworkTuner.exe")))
            {
                installDir = currentDir;
            }
            else
            {
                installDir = GetRegisteredInstallDir();
            }

            if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
            {
                installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RobloxNetworkTuner");
            }

            bool isInsideTarget = false;
            try
            {
                string normCurrent = Path.GetFullPath(currentExe);
                string normTarget = Path.GetFullPath(installDir).TrimEnd('\\') + "\\";
                isInsideTarget = normCurrent.StartsWith(normTarget, StringComparison.OrdinalIgnoreCase);
            }
            catch { }

            if (isInsideTarget)
            {
                try
                {
                    string tempWorker = Path.Combine(Path.GetTempPath(), "RNT_UninstallWorker.exe");
                    File.Copy(currentExe, tempWorker, true);

                    int myPid = Process.GetCurrentProcess().Id;
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = tempWorker;
                    psi.Arguments = string.Format("--uninstall-worker \"{0}\" {1} {2}", installDir, myPid, isSilent ? "--silent" : "");
                    psi.WorkingDirectory = Path.GetTempPath();
                    psi.UseShellExecute = false;
                    Process.Start(psi);

                    Environment.Exit(0);
                    return 0;
                }
                catch { }
            }

            if (isSilent)
            {
                return RunUninstallWorkerSilent(installDir, 0);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupGuiForm(installDir, true, 0));
            return 0;
        }

        private static int RunUninstallWorkerSilent(string installDir, int parentPid)
        {
            try { Environment.CurrentDirectory = Path.GetTempPath(); } catch { }
            if (parentPid > 0)
            {
                try
                {
                    Process parent = Process.GetProcessById(parentPid);
                    if (parent != null && !parent.HasExited)
                    {
                        parent.WaitForExit(5000);
                    }
                }
                catch { }
                Thread.Sleep(400);
            }
            RunUninstallWork(installDir, null);
            return 0;
        }

        private static string GetRegisteredInstallDir()
        {
            try
            {
                using (RegistryKey key = (Registry.LocalMachine.OpenSubKey(UninstallRegSubKey) ?? Registry.CurrentUser.OpenSubKey(UninstallRegSubKey)))
                {
                    if (key != null)
                    {
                        object loc = key.GetValue("InstallLocation");
                        if (loc != null) return loc.ToString();
                    }
                }
            }
            catch { }
            return null;
        }

        #endregion

        #region Embedded Resource & Shortcut Helpers

        public static byte[] ExtractOrDecompressPayload(string pkgResourceName, string rawExeName)
        {
            Assembly asm = Assembly.GetExecutingAssembly();

            Stream s = FindResourceStream(asm, pkgResourceName);
            if (s != null)
            {
                using (s)
                using (GZipStream gz = new GZipStream(s, CompressionMode.Decompress))
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buf = new byte[32768];
                    int read;
                    while ((read = gz.Read(buf, 0, buf.Length)) > 0)
                    {
                        ms.Write(buf, 0, read);
                    }
                    return ms.ToArray();
                }
            }

            s = FindResourceStream(asm, rawExeName);
            if (s != null)
            {
                using (s)
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buf = new byte[32768];
                    int read;
                    while ((read = s.Read(buf, 0, buf.Length)) > 0)
                    {
                        ms.Write(buf, 0, read);
                    }
                    return ms.ToArray();
                }
            }

            if (File.Exists(rawExeName))
            {
                return File.ReadAllBytes(rawExeName);
            }

            return null;
        }

        private static Stream FindResourceStream(Assembly asm, string name)
        {
            Stream s = asm.GetManifestResourceStream(name);
            if (s != null) return s;

            string[] all = asm.GetManifestResourceNames();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].EndsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    return asm.GetManifestResourceStream(all[i]);
                }
            }
            return null;
        }

        public static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string arguments, string description, string iconLocation)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                if (shortcut == null) return;

                Type scType = shortcut.GetType();
                scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });

                if (!string.IsNullOrEmpty(arguments))
                {
                    scType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
                }
                if (!string.IsNullOrEmpty(description))
                {
                    scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
                }
                if (!string.IsNullOrEmpty(iconLocation))
                {
                    scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconLocation });
                }

                scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch { }
        }

        public static void RunSilent(string file, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = file;
                psi.Arguments = args;
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(5000);
                }
            }
            catch { }
        }

        public static string RunCapture(string file, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = file;
                psi.Arguments = args;
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                using (Process p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        string res = p.StandardOutput.ReadToEnd();
                        p.WaitForExit(5000);
                        return res;
                    }
                }
            }
            catch { }
            return "";
        }

        private static void PrintHelp()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" ROBLOX NETWORK TUNER - STANDALONE INSTALLER & SETUP [x64]");
            Console.WriteLine("================================================================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  RobloxNetworkTunerSetup.exe [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  (no args)           Run graphical installer");
            Console.WriteLine("  --silent, -s        Run silent unattended installation");
            Console.WriteLine("  --dir <path>, -d    Specify custom destination directory");
            Console.WriteLine("  --uninstall, -u     Uninstall and restore default network settings");
            Console.WriteLine("  --help, -h, /?      Display this help menu");
            Console.WriteLine();
        }

        #endregion
    }

    #region Setup GUI

    public class SetupGuiForm : Form
    {
        private readonly string targetDir;
        private readonly bool isUninstall;
        private readonly int parentPidToWait;

        // Visual Colors & Obsidian Theme
        private readonly Color colBg = Color.FromArgb(10, 13, 20);            // Deep obsidian
        private readonly Color colSurface = Color.FromArgb(16, 21, 31);       // Card background
        private readonly Color colBorder = Color.FromArgb(28, 38, 56);        // Crisp subtle border
        private readonly Color colCyan = Color.FromArgb(0, 240, 255);         // Electric Cyan #00F0FF
        private readonly Color colEmerald = Color.FromArgb(0, 255, 163);      // Emerald #00FFA3
        private readonly Color colTextMuted = Color.FromArgb(125, 139, 159);  // Steel slate
        private readonly Color colTextDim = Color.FromArgb(80, 94, 115);      // Dark slate
        private readonly Color colTextLight = Color.FromArgb(241, 245, 249);  // Crisp white

        // Typography Fonts
        private readonly Font fontBrand = new Font("Segoe UI", 14.5f, FontStyle.Bold);
        private readonly Font fontTitle = new Font("Segoe UI", 8.0f, FontStyle.Bold);
        private readonly Font fontSubtitle = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        private readonly Font fontStatus = new Font("Segoe UI", 9.0f, FontStyle.Regular);
        private readonly Font fontPercent = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        private readonly Font fontSteps = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        private readonly Font fontBadge = new Font("Segoe UI", 7.0f, FontStyle.Bold);
        private readonly Font fontButton = new Font("Segoe UI", 9.0f, FontStyle.Bold);

        // Animation & State
        private System.Windows.Forms.Timer animTimer;
        private int animTick = 0;
        private float visualProgress = 0.0f;
        private float targetProgress = 0.0f;
        private int currentMilestone = 0;
        private string currentStatus = "Initializing setup components...";
        private bool isComplete = false;
        private bool hasError = false;
        private string errorMessage = "";
        private int countdownSeconds = 3;
        private int countdownTicks = 0;
        private bool autoLaunchCanceled = false;

        // Interactive Button Rectangles
        private Rectangle rectCloseBtn = new Rectangle(498, 8, 32, 24);
        private Rectangle rectLaunchBtn = new Rectangle(325, 316, 175, 36);
        private Rectangle rectDismissBtn = new Rectangle(215, 316, 95, 36);
        private Rectangle rectUninstallCloseBtn = new Rectangle(385, 316, 115, 36);

        private string hoverElement = null; // "win_close", "launch", "dismiss", "un_close"

        #region Native Methods for Window Dragging & Shadow

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        private const int CS_DROPSHADOW = 0x00020000;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        #endregion

        public SetupGuiForm(string targetDirectory, bool uninstallMode, int parentPid)
        {
            targetDir = targetDirectory;
            isUninstall = uninstallMode;
            parentPidToWait = parentPid;

            // Form Properties
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new Size(540, 375);
            this.BackColor = colBg;
            this.DoubleBuffered = true;
            this.ShowInTaskbar = true;
            this.Text = isUninstall ? "Roblox Network Tuner - Uninstaller" : "Roblox Network Tuner - Setup";

            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                this.Icon = Icon.ExtractAssociatedIcon(exePath);
            }
            catch { }

            // Mouse Events
            this.MouseDown += SetupGuiForm_MouseDown;
            this.MouseMove += SetupGuiForm_MouseMove;
            this.MouseLeave += SetupGuiForm_MouseLeave;
            this.MouseUp += SetupGuiForm_MouseUp;

            // Animation Timer (16ms = ~60 FPS)
            animTimer = new System.Windows.Forms.Timer();
            animTimer.Interval = 16;
            animTimer.Tick += AnimTimer_Tick;
            animTimer.Start();

            // Launch background worker thread
            Thread workerThread = new Thread(WorkerRun);
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        private void WorkerRun()
        {
            // If worker mode with parent PID, wait for parent to exit
            if (parentPidToWait > 0)
            {
                try
                {
                    Process parent = Process.GetProcessById(parentPidToWait);
                    if (parent != null && !parent.HasExited)
                    {
                        parent.WaitForExit(5000);
                    }
                }
                catch { }
                Thread.Sleep(300);
            }

            try
            {
                if (isUninstall)
                {
                    Program.RunUninstallWork(targetDir, OnWorkerProgress);
                }
                else
                {
                    Program.RunInstallWork(targetDir, OnWorkerProgress);
                }

                // Set complete
                this.BeginInvoke(new Action(delegate
                {
                    targetProgress = 1.0f;
                    isComplete = true;
                    currentStatus = isUninstall ? "Uninstallation completed successfully." : "Installation completed successfully!";
                }));
            }
            catch (Exception ex)
            {
                this.BeginInvoke(new Action(delegate
                {
                    hasError = true;
                    errorMessage = ex.Message;
                    currentStatus = "Error: " + ex.Message;
                }));
            }
        }

        private void OnWorkerProgress(float progress, int milestone, string text)
        {
            if (this.IsDisposed) return;
            this.BeginInvoke(new Action(delegate
            {
                targetProgress = progress;
                currentMilestone = milestone;
                currentStatus = text;
            }));
        }

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            animTick++;

            // Smooth progress interpolation (lerp)
            float diff = targetProgress - visualProgress;
            if (Math.Abs(diff) > 0.001f)
            {
                visualProgress += diff * 0.12f;
            }
            else
            {
                visualProgress = targetProgress;
            }

            // Auto-launch countdown handling
            if (isComplete && !isUninstall && !autoLaunchCanceled)
            {
                countdownTicks++;
                if (countdownTicks >= 60) // 1 second elapsed (60 frames @ 16ms)
                {
                    countdownTicks = 0;
                    countdownSeconds--;
                    if (countdownSeconds <= 0)
                    {
                        animTimer.Stop();
                        LaunchTunerAndExit();
                        return;
                    }
                }
            }

            this.Invalidate();
        }

        private void SetupGuiForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Y < 42 && !rectCloseBtn.Contains(e.Location))
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void SetupGuiForm_MouseMove(object sender, MouseEventArgs e)
        {
            string oldHover = hoverElement;
            if (rectCloseBtn.Contains(e.Location))
            {
                hoverElement = "win_close";
            }
            else if (isComplete && !isUninstall && rectLaunchBtn.Contains(e.Location))
            {
                hoverElement = "launch";
            }
            else if (isComplete && !isUninstall && rectDismissBtn.Contains(e.Location))
            {
                hoverElement = "dismiss";
            }
            else if (isComplete && isUninstall && rectUninstallCloseBtn.Contains(e.Location))
            {
                hoverElement = "un_close";
            }
            else
            {
                hoverElement = null;
            }

            if (oldHover != hoverElement)
            {
                this.Invalidate();
            }
        }

        private void SetupGuiForm_MouseLeave(object sender, EventArgs e)
        {
            if (hoverElement != null)
            {
                hoverElement = null;
                this.Invalidate();
            }
        }

        private void SetupGuiForm_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (rectCloseBtn.Contains(e.Location))
                {
                    this.Close();
                }
                else if (isComplete && !isUninstall && rectLaunchBtn.Contains(e.Location))
                {
                    autoLaunchCanceled = true;
                    LaunchTunerAndExit();
                }
                else if (isComplete && !isUninstall && rectDismissBtn.Contains(e.Location))
                {
                    autoLaunchCanceled = true;
                    this.Close();
                }
                else if (isComplete && isUninstall && rectUninstallCloseBtn.Contains(e.Location))
                {
                    this.Close();
                }
            }
        }

        private void LaunchTunerAndExit()
        {
            try
            {
                string tunerExe = Path.Combine(targetDir, "RobloxNetworkTuner.exe");
                if (File.Exists(tunerExe))
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = tunerExe;
                    psi.WorkingDirectory = targetDir;
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
            }
            catch { }
            this.Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;

            // 1. Base Background & Gradient Glow
            using (SolidBrush bBg = new SolidBrush(colBg))
            {
                g.FillRectangle(bBg, 0, 0, w, h);
            }

            // Subtle top horizontal line with cyan glow gradient
            using (LinearGradientBrush lgbTop = new LinearGradientBrush(new Point(0, 0), new Point(w, 0), Color.Transparent, Color.Transparent))
            {
                ColorBlend cb = new ColorBlend(3);
                cb.Colors = new Color[] { Color.FromArgb(0, colCyan), Color.FromArgb(180, colCyan), Color.FromArgb(0, colCyan) };
                cb.Positions = new float[] { 0.0f, 0.5f, 1.0f };
                lgbTop.InterpolationColors = cb;
                using (Pen pTop = new Pen(lgbTop, 1.5f))
                {
                    g.DrawLine(pTop, 0, 0, w, 0);
                }
            }

            // Precision 1px Outer Border
            using (Pen pBorder = new Pen(colBorder, 1.0f))
            {
                g.DrawRectangle(pBorder, 0, 0, w - 1, h - 1);
            }

            // 2. Title Bar Header
            using (SolidBrush bTitle = new SolidBrush(colCyan))
            {
                g.DrawString("ROBLOX NETWORK TUNER", fontTitle, bTitle, 16, 12);
            }
            using (SolidBrush bSub = new SolidBrush(colTextDim))
            {
                string modeTag = isUninstall ? " // UNINSTALLER & RESTORER" : " // SETUP BOOTSTRAPPER";
                g.DrawString(modeTag, fontTitle, bSub, 172, 12);
            }

            // Window Close Button [✕]
            bool isCloseHover = (hoverElement == "win_close");
            if (isCloseHover)
            {
                using (SolidBrush bCloseHover = new SolidBrush(Color.FromArgb(225, 29, 72)))
                {
                    g.FillRectangle(bCloseHover, rectCloseBtn);
                }
            }
            using (Pen pClose = new Pen(isCloseHover ? Color.White : colTextMuted, 1.6f))
            {
                int cx = rectCloseBtn.X + rectCloseBtn.Width / 2;
                int cy = rectCloseBtn.Y + rectCloseBtn.Height / 2;
                g.DrawLine(pClose, cx - 4, cy - 4, cx + 4, cy + 4);
                g.DrawLine(pClose, cx + 4, cy - 4, cx - 4, cy + 4);
            }

            // 3. Hero Brand Mark & Aura (Hexagon Shield with Pulsing Glow)
            DrawHeroBrand(g, 32, 48);

            // 4. Milestone Step Indicators
            DrawMilestones(g, 136);

            // 5. Progress Area / Completion Card
            if (!isComplete && !hasError)
            {
                DrawProgressSection(g, 204, w);
            }
            else if (isComplete)
            {
                DrawCompletionSection(g, 198, w);
            }
            else if (hasError)
            {
                DrawErrorSection(g, 198, w);
            }

            // 6. Footer / Bottom Action Controls
            DrawFooter(g, 304, w);
        }

        private void DrawHeroBrand(Graphics g, int x, int y)
        {
            int hexSize = 58;
            PointF center = new PointF(x + hexSize / 2f, y + hexSize / 2f);

            // Breathing aura glow behind hexagon
            float glowSine = (float)(Math.Sin(animTick * 0.08) * 0.5 + 0.5);
            int glowAlpha = (int)(20 + 35 * glowSine);
            using (SolidBrush bGlow = new SolidBrush(Color.FromArgb(glowAlpha, colCyan)))
            {
                g.FillEllipse(bGlow, center.X - 38, center.Y - 38, 76, 76);
            }

            // Outer Hexagon Shield Path
            PointF[] hexPoints = GetHexagonPoints(center, 27f);
            using (SolidBrush bHexFill = new SolidBrush(Color.FromArgb(14, 19, 29)))
            {
                g.FillPolygon(bHexFill, hexPoints);
            }

            using (LinearGradientBrush lgbHex = new LinearGradientBrush(new Point(x, y), new Point(x + hexSize, y + hexSize), colCyan, colEmerald))
            {
                using (Pen pHex = new Pen(lgbHex, 2.0f))
                {
                    g.DrawPolygon(pHex, hexPoints);
                }
            }

            // Inner Network Fiber Lightning Pulse
            using (Pen pPulse = new Pen(colCyan, 1.8f))
            {
                pPulse.StartCap = LineCap.Round;
                pPulse.EndCap = LineCap.Round;
                PointF pTop = new PointF(center.X, center.Y - 14);
                PointF pMid1 = new PointF(center.X - 5, center.Y - 1);
                PointF pMid2 = new PointF(center.X + 6, center.Y + 2);
                PointF pBot = new PointF(center.X, center.Y + 14);

                g.DrawLine(pPulse, pTop, pMid1);
                g.DrawLine(pPulse, pMid1, pMid2);
                g.DrawLine(pPulse, pMid2, pBot);
            }

            // Micro fiber nodes
            using (SolidBrush bNode = new SolidBrush(Color.White))
            {
                g.FillEllipse(bNode, center.X - 2.5f, center.Y - 16.5f, 5, 5);
                g.FillEllipse(bNode, center.X - 2.5f, center.Y + 11.5f, 5, 5);
            }

            // Hero Typography Beside Hexagon
            int textX = x + hexSize + 16;
            using (SolidBrush bHero = new SolidBrush(colTextLight))
            {
                g.DrawString(Program.AppTitle, fontBrand, bHero, textX, y - 2);
            }

            using (SolidBrush bSub = new SolidBrush(colTextMuted))
            {
                string tag = isUninstall
                    ? "Remove application and reset network settings"
                    : "Low-latency network optimizer for Roblox";
                g.DrawString(tag, fontSubtitle, bSub, textX + 1, y + 26);
            }

            // Architecture Pill Badge
            int badgeY = y + 47;
            int badgeW = isUninstall ? 115 : 100;
            int badgeH = 18;
            Rectangle rectBadge = new Rectangle(textX + 1, badgeY, badgeW, badgeH);

            using (SolidBrush bBadgeBg = new SolidBrush(Color.FromArgb(12, 28, 30)))
            {
                g.FillRectangle(bBadgeBg, rectBadge);
            }
            using (Pen pBadge = new Pen(Color.FromArgb(18, 70, 72), 1.0f))
            {
                g.DrawRectangle(pBadge, rectBadge);
            }
            using (SolidBrush bBadgeText = new SolidBrush(colEmerald))
            {
                string badgeStr = isUninstall ? "UNINSTALL • x64" : "SETUP • x64";
                g.DrawString(badgeStr, fontBadge, bBadgeText, rectBadge.X + 7, rectBadge.Y + 3);
            }
        }

        private void DrawMilestones(Graphics g, int y)
        {
            string[] installLabels = new string[] { "SYSTEM", "FILES", "SHORTCUTS", "FINISH" };
            string[] uninstallLabels = new string[] { "PROCESS", "TIMER", "NETWORK", "CLEANUP" };
            string[] labels = isUninstall ? uninstallLabels : installLabels;

            int totalNodes = 4;
            int startX = 60;
            int nodeSpacing = 120;
            int nodeRadius = 11;

            // Milestone Track Connecting Lines
            for (int i = 0; i < totalNodes - 1; i++)
            {
                int x1 = startX + i * nodeSpacing + nodeRadius + 3;
                int x2 = startX + (i + 1) * nodeSpacing - nodeRadius - 3;
                int lineY = y + nodeRadius;

                bool isPassed = (i < currentMilestone);
                using (Pen pLine = new Pen(isPassed ? colCyan : colBorder, 1.8f))
                {
                    g.DrawLine(pLine, x1, lineY, x2, lineY);
                }
            }

            // Milestone Nodes
            for (int i = 0; i < totalNodes; i++)
            {
                int nx = startX + i * nodeSpacing;
                int ny = y;
                Rectangle rectNode = new Rectangle(nx - nodeRadius, ny, nodeRadius * 2, nodeRadius * 2);

                if (i < currentMilestone || (isComplete && i <= currentMilestone))
                {
                    // Completed: Emerald circle with dark checkmark
                    using (SolidBrush bComp = new SolidBrush(colEmerald))
                    {
                        g.FillEllipse(bComp, rectNode);
                    }
                    using (Pen pCheck = new Pen(colBg, 1.8f))
                    {
                        pCheck.StartCap = LineCap.Round;
                        pCheck.EndCap = LineCap.Round;
                        g.DrawLine(pCheck, rectNode.X + 6, rectNode.Y + 11, rectNode.X + 10, rectNode.Y + 15);
                        g.DrawLine(pCheck, rectNode.X + 10, rectNode.Y + 15, rectNode.X + 16, rectNode.Y + 7);
                    }
                }
                else if (i == currentMilestone && !isComplete)
                {
                    // Active: Pulsing Cyan Circle with white inner dot
                    float pulse = (float)(Math.Sin(animTick * 0.15) * 0.5 + 0.5);
                    using (SolidBrush bAura = new SolidBrush(Color.FromArgb((int)(40 + 50 * pulse), colCyan)))
                    {
                        g.FillEllipse(bAura, rectNode.X - 3, rectNode.Y - 3, rectNode.Width + 6, rectNode.Height + 6);
                    }
                    using (SolidBrush bActive = new SolidBrush(colCyan))
                    {
                        g.FillEllipse(bActive, rectNode);
                    }
                    using (SolidBrush bCenter = new SolidBrush(Color.White))
                    {
                        g.FillEllipse(bCenter, rectNode.X + 7, rectNode.Y + 7, 8, 8);
                    }
                }
                else
                {
                    // Pending: Dim dark circle with border
                    using (SolidBrush bPending = new SolidBrush(colSurface))
                    {
                        g.FillEllipse(bPending, rectNode);
                    }
                    using (Pen pPending = new Pen(colBorder, 1.5f))
                    {
                        g.DrawEllipse(pPending, rectNode);
                    }
                    using (SolidBrush bDot = new SolidBrush(colTextDim))
                    {
                        g.FillEllipse(bDot, rectNode.X + 8, rectNode.Y + 8, 6, 6);
                    }
                }

                // Node Text Label
                bool isHigh = (i <= currentMilestone);
                using (SolidBrush bLbl = new SolidBrush(isHigh ? colTextLight : colTextDim))
                {
                    SizeF sz = g.MeasureString(labels[i], fontSteps);
                    g.DrawString(labels[i], fontSteps, bLbl, nx - sz.Width / 2f, ny + nodeRadius * 2 + 5);
                }
            }
        }

        private void DrawProgressSection(Graphics g, int y, int totalW)
        {
            int padX = 35;
            int barW = totalW - (padX * 2);
            int barH = 10;

            // Status message readout
            using (SolidBrush bStat = new SolidBrush(colTextLight))
            {
                g.DrawString(currentStatus, fontStatus, bStat, padX, y);
            }

            // Percentage Readout
            int percent = (int)(Math.Min(1.0f, Math.Max(0.0f, visualProgress)) * 100f);
            string pctStr = string.Format("{0}%", percent);
            using (SolidBrush bPct = new SolidBrush(colCyan))
            {
                SizeF szPct = g.MeasureString(pctStr, fontPercent);
                g.DrawString(pctStr, fontPercent, bPct, padX + barW - szPct.Width, y - 2);
            }

            // Progress Bar Track
            int trackY = y + 26;
            Rectangle rectTrack = new Rectangle(padX, trackY, barW, barH);
            using (SolidBrush bTrack = new SolidBrush(Color.FromArgb(14, 18, 28)))
            {
                FillRoundedRectangle(g, bTrack, rectTrack, 4);
            }
            using (Pen pTrack = new Pen(colBorder, 1.0f))
            {
                DrawRoundedRectangle(g, pTrack, rectTrack, 4);
            }

            // Progress Bar Fill with Electric Cyan -> Emerald Gradient
            int fillW = (int)(barW * Math.Min(1.0f, Math.Max(0.0f, visualProgress)));
            if (fillW > 4)
            {
                Rectangle rectFill = new Rectangle(padX, trackY, fillW, barH);
                using (LinearGradientBrush lgbFill = new LinearGradientBrush(new Point(padX, trackY), new Point(padX + barW, trackY), colCyan, colEmerald))
                {
                    FillRoundedRectangle(g, lgbFill, rectFill, 4);
                }

                // Animated Sweeping Shimmer Beam
                int shimmerSpan = 140;
                int shimmerCycle = (animTick * 6) % (barW + shimmerSpan * 2);
                int shimmerX = padX - shimmerSpan + shimmerCycle;

                int sStart = Math.Max(padX, shimmerX);
                int sEnd = Math.Min(padX + fillW, shimmerX + shimmerSpan);
                if (sEnd > sStart)
                {
                    Rectangle rectShimmer = new Rectangle(sStart, trackY, sEnd - sStart, barH);
                    using (LinearGradientBrush lgbShimmer = new LinearGradientBrush(rectShimmer, Color.Transparent, Color.Transparent, LinearGradientMode.Horizontal))
                    {
                        ColorBlend cb = new ColorBlend(3);
                        cb.Colors = new Color[] { Color.FromArgb(0, Color.White), Color.FromArgb(120, Color.White), Color.FromArgb(0, Color.White) };
                        cb.Positions = new float[] { 0.0f, 0.5f, 1.0f };
                        lgbShimmer.InterpolationColors = cb;
                        FillRoundedRectangle(g, lgbShimmer, rectShimmer, 4);
                    }
                }
            }

            // Sub-status destination info
            using (SolidBrush bDest = new SolidBrush(colTextDim))
            {
                string destText = isUninstall
                    ? "Target Directory: " + (!string.IsNullOrEmpty(targetDir) ? targetDir : "Default")
                    : "Destination: " + targetDir;
                g.DrawString(destText, fontBadge, bDest, padX, trackY + 16);
            }
        }

        private void DrawCompletionSection(Graphics g, int y, int totalW)
        {
            int padX = 35;
            int cardW = totalW - (padX * 2);
            int cardH = 80;

            Rectangle rectCard = new Rectangle(padX, y, cardW, cardH);
            using (SolidBrush bCard = new SolidBrush(colSurface))
            {
                FillRoundedRectangle(g, bCard, rectCard, 6);
            }
            using (Pen pCard = new Pen(Color.FromArgb(20, 80, 60), 1.0f))
            {
                DrawRoundedRectangle(g, pCard, rectCard, 6);
            }

            // Emerald Check Badge (36x36)
            int badgeX = padX + 16;
            int badgeY = y + 22;
            using (SolidBrush bBadge = new SolidBrush(Color.FromArgb(16, 50, 40)))
            {
                g.FillEllipse(bBadge, badgeX, badgeY, 36, 36);
            }
            using (Pen pBadge = new Pen(colEmerald, 1.5f))
            {
                g.DrawEllipse(pBadge, badgeX, badgeY, 36, 36);
            }
            using (Pen pCheck = new Pen(colEmerald, 2.2f))
            {
                pCheck.StartCap = LineCap.Round;
                pCheck.EndCap = LineCap.Round;
                g.DrawLine(pCheck, badgeX + 10, badgeY + 18, badgeX + 16, badgeY + 24);
                g.DrawLine(pCheck, badgeX + 16, badgeY + 24, badgeX + 26, badgeY + 12);
            }

            // Completion Headings
            int txtX = badgeX + 48;
            using (SolidBrush bTitle = new SolidBrush(colEmerald))
            {
                string title = isUninstall ? "Uninstallation Complete" : "Installation Complete";
                g.DrawString(title, fontBrand, bTitle, txtX, y + 14);
            }
            using (SolidBrush bMsg = new SolidBrush(colTextMuted))
            {
                string desc = isUninstall
                    ? "Windows network and registry defaults restored."
                    : "Roblox Network Tuner is installed and ready to use.";
                g.DrawString(desc, fontSubtitle, bMsg, txtX, y + 42);
            }
        }

        private void DrawErrorSection(Graphics g, int y, int totalW)
        {
            int padX = 35;
            int cardW = totalW - (padX * 2);
            int cardH = 80;

            Rectangle rectCard = new Rectangle(padX, y, cardW, cardH);
            using (SolidBrush bCard = new SolidBrush(Color.FromArgb(30, 15, 20)))
            {
                FillRoundedRectangle(g, bCard, rectCard, 6);
            }
            using (Pen pCard = new Pen(Color.FromArgb(225, 29, 72), 1.0f))
            {
                DrawRoundedRectangle(g, pCard, rectCard, 6);
            }

            using (SolidBrush bErr = new SolidBrush(Color.FromArgb(255, 100, 100)))
            {
                g.DrawString("Setup encountered an issue:", fontTitle, bErr, padX + 16, y + 16);
                g.DrawString(errorMessage, fontSubtitle, bErr, padX + 16, y + 38);
            }
        }

        private void DrawFooter(Graphics g, int y, int totalW)
        {
            // Separator Line
            using (Pen pSep = new Pen(colBorder, 1.0f))
            {
                g.DrawLine(pSep, 25, y, totalW - 25, y);
            }

            if (!isComplete && !hasError)
            {
                // Active State Message
                using (SolidBrush bWait = new SolidBrush(colTextMuted))
                {
                    g.DrawString("Applying configurations... Please wait.", fontSubtitle, bWait, 35, y + 20);
                }

                // High-precision rotating activity dot pulse
                int dotCx = totalW - 48;
                int dotCy = y + 27;
                float rotAngle = (animTick * 8f) % 360f;
                double rad = rotAngle * Math.PI / 180.0;
                float px = (float)(dotCx + Math.Cos(rad) * 8.0);
                float py = (float)(dotCy + Math.Sin(rad) * 8.0);

                using (SolidBrush bOrbit = new SolidBrush(colCyan))
                {
                    g.FillEllipse(bOrbit, px - 3, py - 3, 6, 6);
                }
                using (SolidBrush bCore = new SolidBrush(Color.FromArgb(60, colCyan)))
                {
                    g.FillEllipse(bCore, dotCx - 4, dotCy - 4, 8, 8);
                }
            }
            else if (isComplete && !isUninstall)
            {
                // Secondary "Close" Button
                bool isDismissHover = (hoverElement == "dismiss");
                using (SolidBrush bDismiss = new SolidBrush(isDismissHover ? Color.FromArgb(28, 38, 56) : colSurface))
                {
                    FillRoundedRectangle(g, bDismiss, rectDismissBtn, 5);
                }
                using (Pen pDismiss = new Pen(isDismissHover ? colTextMuted : colBorder, 1.0f))
                {
                    DrawRoundedRectangle(g, pDismiss, rectDismissBtn, 5);
                }
                using (SolidBrush bDismissText = new SolidBrush(isDismissHover ? Color.White : colTextMuted))
                {
                    SizeF sz = g.MeasureString("Close", fontButton);
                    g.DrawString("Close", fontButton, bDismissText, rectDismissBtn.X + (rectDismissBtn.Width - sz.Width) / 2f, rectDismissBtn.Y + (rectDismissBtn.Height - sz.Height) / 2f);
                }

                // Primary "LAUNCH NOW" Button
                bool isLaunchHover = (hoverElement == "launch");
                string launchText = autoLaunchCanceled
                    ? "LAUNCH NOW"
                    : string.Format("LAUNCH NOW ({0}s)", Math.Max(0, countdownSeconds));

                using (LinearGradientBrush lgbBtn = new LinearGradientBrush(rectLaunchBtn, colCyan, colEmerald, LinearGradientMode.Horizontal))
                {
                    FillRoundedRectangle(g, lgbBtn, rectLaunchBtn, 5);
                }
                if (isLaunchHover)
                {
                    using (SolidBrush bHoverWhite = new SolidBrush(Color.FromArgb(40, Color.White)))
                    {
                        FillRoundedRectangle(g, bHoverWhite, rectLaunchBtn, 5);
                    }
                }
                using (SolidBrush bBtnText = new SolidBrush(Color.FromArgb(10, 14, 22)))
                {
                    SizeF sz = g.MeasureString(launchText, fontButton);
                    g.DrawString(launchText, fontButton, bBtnText, rectLaunchBtn.X + (rectLaunchBtn.Width - sz.Width) / 2f, rectLaunchBtn.Y + (rectLaunchBtn.Height - sz.Height) / 2f);
                }
            }
            else if (isComplete && isUninstall)
            {
                // Uninstallation Complete "CLOSE" Button
                bool isUnCloseHover = (hoverElement == "un_close");
                using (LinearGradientBrush lgbBtn = new LinearGradientBrush(rectUninstallCloseBtn, colCyan, colEmerald, LinearGradientMode.Horizontal))
                {
                    FillRoundedRectangle(g, lgbBtn, rectUninstallCloseBtn, 5);
                }
                if (isUnCloseHover)
                {
                    using (SolidBrush bWhite = new SolidBrush(Color.FromArgb(40, Color.White)))
                    {
                        FillRoundedRectangle(g, bWhite, rectUninstallCloseBtn, 5);
                    }
                }
                using (SolidBrush bBtnText = new SolidBrush(Color.FromArgb(10, 14, 22)))
                {
                    SizeF sz = g.MeasureString("CLOSE", fontButton);
                    g.DrawString("CLOSE", fontButton, bBtnText, rectUninstallCloseBtn.X + (rectUninstallCloseBtn.Width - sz.Width) / 2f, rectUninstallCloseBtn.Y + (rectUninstallCloseBtn.Height - sz.Height) / 2f);
                }
            }
            else if (hasError)
            {
                // Error Close Button
                using (SolidBrush bErrClose = new SolidBrush(colSurface))
                {
                    FillRoundedRectangle(g, bErrClose, rectDismissBtn, 5);
                }
                using (Pen pErrClose = new Pen(colBorder, 1.0f))
                {
                    DrawRoundedRectangle(g, pErrClose, rectDismissBtn, 5);
                }
                using (SolidBrush bTxt = new SolidBrush(Color.White))
                {
                    SizeF sz = g.MeasureString("Close", fontButton);
                    g.DrawString("Close", fontButton, bTxt, rectDismissBtn.X + (rectDismissBtn.Width - sz.Width) / 2f, rectDismissBtn.Y + (rectDismissBtn.Height - sz.Height) / 2f);
                }
            }
        }

        #region Geometry & Drawing Utilities

        private static PointF[] GetHexagonPoints(PointF center, float radius)
        {
            PointF[] pts = new PointF[6];
            for (int i = 0; i < 6; i++)
            {
                double angleDeg = 60 * i - 30; // Pointy top
                double angleRad = Math.PI / 180.0 * angleDeg;
                pts[i] = new PointF(
                    (float)(center.X + radius * Math.Cos(angleRad)),
                    (float)(center.Y + radius * Math.Sin(angleRad))
                );
            }
            return pts;
        }

        private static void FillRoundedRectangle(Graphics g, Brush brush, Rectangle bounds, int cornerRadius)
        {
            using (GraphicsPath path = CreateRoundedRectanglePath(bounds, cornerRadius))
            {
                g.FillPath(brush, path);
            }
        }

        private static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle bounds, int cornerRadius)
        {
            using (GraphicsPath path = CreateRoundedRectanglePath(bounds, cornerRadius))
            {
                g.DrawPath(pen, path);
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            // Top left arc
            path.AddArc(arc, 180, 90);

            // Top right arc
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom right arc
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom left arc
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        #endregion
    }

    #endregion
}
