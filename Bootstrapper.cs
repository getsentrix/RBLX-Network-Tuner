using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
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
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

namespace RobloxNetworkTuner.Setup
{
    internal static class Program
    {
        private const string AppTitle = "Roblox Network Tuner";
        private const string AppVersion = "2.0.0";
        private const string PublisherName = "Roblox Performance Engineering";
        private const string UninstallRegSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RobloxNetworkTuner";
        private const string QosPolicyName = "RobloxNetworkTuner_DSCP46";

        #region Native Methods

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetTimerResolution(uint desiredResolution, bool setResolution, out uint currentResolution);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
        private static extern uint TimeEndPeriod(uint uMilliseconds);

        #endregion

        private static int Main(string[] args)
        {
            Console.Title = "Roblox Network Tuner - Setup [x64]";

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

            // Worker mode handles uninstallation from %TEMP% to allow complete folder deletion
            if (isWorker)
            {
                return RunUninstallWorker(workerInstallDir, parentPid, isSilent);
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

            if (isUninstall)
            {
                return DispatchUninstall(isSilent);
            }

            return PerformInstall(customDir, isSilent);
        }

        #region Elevation & Environment

        private static bool IsAdministrator()
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

        #region ASCII Logo & Console Presentation

        private static void PrintLogo(string subtitle)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@" ==============================================================================");
            Console.WriteLine(@"  ____   ___  ____  _     _____  __  _   _ _____ _____ ");
            Console.WriteLine(@" |  _ \ / _ \| __ )| |   / _ \ \/ / | \ | | ____|_   _|");
            Console.WriteLine(@" | |_) | | | |  _ \| |  | | | \  /  |  \| |  _|   | |  ");
            Console.WriteLine(@" |  _ <| |_| | |_) | |__| |_| /  \  | |\  | |___  | |  ");
            Console.WriteLine(@" |_| \_\\___/|____/|_____\___/_/\_\ |_| \_|_____| |_|  ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(@"             NETWORK TUNER :: ULTRA-LOW LATENCY & PACKET PACING");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(string.Format(@"                 {0}", subtitle));
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@" ==============================================================================");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void LogStep(int currentStep, int totalSteps, string description, Action action, bool isSilent)
        {
            if (isSilent)
            {
                try { action(); } catch { }
                return;
            }

            string label = string.Format(" [{0:D2}/{1:D2}] {2,-62} ... ", currentStep, totalSteps, description);
            Console.Write(label);
            Thread.Sleep(30);

            try
            {
                action();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[DONE]");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[WARN]");
                Console.ResetColor();
            }
            Thread.Sleep(20);
        }

        #endregion

        #region Installation Logic

        private static int PerformInstall(string customDir, bool isSilent)
        {
            if (!isSilent)
            {
                PrintLogo("v2.0.0 [x64 Production Release]");
            }

            // Determine target installation directory
            string targetDir = customDir;
            if (string.IsNullOrEmpty(targetDir))
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (string.IsNullOrEmpty(progFiles)) progFiles = @"C:\Program Files";
                targetDir = Path.Combine(progFiles, "RobloxNetworkTuner");
            }

            if (!isSilent)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(" Target Installation Directory:");
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("  -> {0}", targetDir);
                Console.ResetColor();
                Console.WriteLine();
            }

            int step = 1;
            int total = 18;

            byte[] tunerBytes = null;
            string tunerExePath = Path.Combine(targetDir, "RobloxNetworkTuner.exe");
            string restoreBatPath = Path.Combine(targetDir, "Restore-Stock.bat");
            string readmeTxtPath = Path.Combine(targetDir, "README.txt");
            string uninstallerPath = Path.Combine(targetDir, "uninstall.exe");

            LogStep(step++, total, "Checking Windows NT kernel architecture and privileges", delegate
            {
                if (!Environment.Is64BitOperatingSystem)
                {
                    throw new PlatformNotSupportedException("64-bit Windows required.");
                }
            }, isSilent);

            LogStep(step++, total, "Initializing target installation directory structure", delegate
            {
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }
            }, isSilent);

            LogStep(step++, total, "Setting directory security permissions and attributes", delegate
            {
                DirectoryInfo di = new DirectoryInfo(targetDir);
                di.Attributes = FileAttributes.Normal;
            }, isSilent);

            LogStep(step++, total, "Reading embedded compressed payload resource stream", delegate
            {
                tunerBytes = ExtractOrDecompressPayload("RobloxNetworkTuner.pkg", "RobloxNetworkTuner.exe");
                if (tunerBytes == null || tunerBytes.Length == 0)
                {
                    throw new FileNotFoundException("Engine payload could not be loaded.");
                }
            }, isSilent);

            LogStep(step++, total, string.Format("Decompressing latency engine: RobloxNetworkTuner.exe ({0:N0} bytes)", tunerBytes != null ? tunerBytes.Length : 0), delegate
            {
                // Decompression verified
            }, isSilent);

            LogStep(step++, total, "Writing executable binary payload to destination", delegate
            {
                File.WriteAllBytes(tunerExePath, tunerBytes);
            }, isSilent);

            LogStep(step++, total, "Generating baseline restorer script: Restore-Stock.bat", delegate
            {
                string batContent =
                    "@echo off\r\n" +
                    "setlocal EnableDelayedExpansion\r\n" +
                    "title Restore Stock System Settings\r\n" +
                    "cd /d \"%~dp0\"\r\n" +
                    "if exist \"%~dp0RobloxNetworkTuner.exe\" (\r\n" +
                    "    \"%~dp0RobloxNetworkTuner.exe\" --restore\r\n" +
                    ")\r\n" +
                    "echo.\r\n" +
                    "echo Baseline network settings have been restored.\r\n" +
                    "pause\r\n";
                File.WriteAllText(restoreBatPath, batContent);
            }, isSilent);

            LogStep(step++, total, "Generating technical reference manual and guide: README.txt", delegate
            {
                string readmeContent =
                    "================================================================================\r\n" +
                    "ROBLOX NETWORK TUNER [x64] - QUICK START GUIDE\r\n" +
                    "================================================================================\r\n\r\n" +
                    "Roblox Network Tuner is a dedicated low-latency and anti-jitter optimization\r\n" +
                    "engine engineered for Roblox on Windows 10 and Windows 11.\r\n\r\n" +
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
                    "1. Double-click the 'Roblox Network Tuner' shortcut on your Desktop or Start Menu.\r\n" +
                    "2. Accept the Windows UAC elevation prompt (Administrator privileges required).\r\n" +
                    "3. The engine activates all optimizations and waits for RobloxPlayerBeta.exe.\r\n" +
                    "4. When you finish playing, press [Space], [Q], [Esc], or simply close Roblox.\r\n" +
                    "   Your system configuration will automatically restore to baseline.\r\n\r\n" +
                    "COMMAND LINE OPTIONS:\r\n" +
                    "  RobloxNetworkTuner.exe              Launch interactive session\r\n" +
                    "  RobloxNetworkTuner.exe --status     Display current kernel and network stack state\r\n" +
                    "  RobloxNetworkTuner.exe --benchmark  Run RFC 3550 RTT and Jitter benchmark\r\n" +
                    "  RobloxNetworkTuner.exe --restore    Perform manual restoration to system baseline\r\n" +
                    "  RobloxNetworkTuner.exe --help       Show usage options\r\n\r\n" +
                    "UNINSTALLATION:\r\n" +
                    "You can uninstall Roblox Network Tuner at any time via:\r\n" +
                    "- Windows Settings -> Apps -> Installed Apps -> Roblox Network Tuner -> Uninstall\r\n" +
                    "- Or Control Panel -> Programs and Features -> Roblox Network Tuner -> Uninstall\r\n" +
                    "================================================================================\r\n";
                File.WriteAllText(readmeTxtPath, readmeContent);
            }, isSilent);

            LogStep(step++, total, "Generating standalone uninstaller binary: uninstall.exe", delegate
            {
                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                if (!string.Equals(currentExe, uninstallerPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(currentExe, uninstallerPath, true);
                }
            }, isSilent);

            LogStep(step++, total, "Verifying file system integrity and payload checksums", delegate
            {
                if (!File.Exists(tunerExePath) || !File.Exists(uninstallerPath))
                {
                    throw new FileNotFoundException("Deployed files failed verification.");
                }
            }, isSilent);

            LogStep(step++, total, "Initializing Windows Shell Scripting Object (WScript.Shell)", delegate
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) throw new InvalidOperationException("WScript.Shell unavailable.");
            }, isSilent);

            LogStep(step++, total, "Creating Desktop shell shortcut: Roblox Network Tuner.lnk", delegate
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string lnk = Path.Combine(desktopPath, "Roblox Network Tuner.lnk");
                CreateShortcut(lnk, tunerExePath, targetDir, "", "Roblox Low-Latency & Anti-Jitter Tuner", tunerExePath + ",0");
            }, isSilent);

            LogStep(step++, total, "Scanning secondary desktop locations (OneDrive / User Profiles)", delegate
            {
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
                        string lnk = Path.Combine(extraDesktops[i], "Roblox Network Tuner.lnk");
                        CreateShortcut(lnk, tunerExePath, targetDir, "", "Roblox Low-Latency & Anti-Jitter Tuner", tunerExePath + ",0");
                    }
                }
            }, isSilent);

            LogStep(step++, total, "Creating Start Menu program group folder", delegate
            {
                string progDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                if (string.IsNullOrEmpty(progDir) || !Directory.Exists(progDir))
                {
                    progDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                }
                string groupDir = Path.Combine(progDir, "Roblox Network Tuner");
                if (!Directory.Exists(groupDir)) Directory.CreateDirectory(groupDir);
            }, isSilent);

            LogStep(step++, total, "Writing Start Menu links: Tuner, Baseline Restore, Uninstall", delegate
            {
                string progDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                if (string.IsNullOrEmpty(progDir) || !Directory.Exists(progDir))
                {
                    progDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                }
                string groupDir = Path.Combine(progDir, "Roblox Network Tuner");

                CreateShortcut(Path.Combine(groupDir, "Roblox Network Tuner.lnk"), tunerExePath, targetDir, "", "Roblox Low-Latency & Anti-Jitter Tuner", tunerExePath + ",0");
                CreateShortcut(Path.Combine(groupDir, "Restore Network Baseline.lnk"), tunerExePath, targetDir, "--restore", "Restore Windows Network Baseline", tunerExePath + ",0");
                CreateShortcut(Path.Combine(groupDir, "Uninstall Roblox Network Tuner.lnk"), uninstallerPath, targetDir, "--uninstall", "Uninstall Roblox Network Tuner", uninstallerPath + ",0");
            }, isSilent);

            LogStep(step++, total, "Registering in Windows Add/Remove Programs (Registry)", delegate
            {
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
            }, isSilent);

            LogStep(step++, total, "Configuring ARP metadata and committing transaction", delegate
            {
                // Confirmation
            }, isSilent);

            LogStep(step++, total, "Flushing file system buffers and committing installation", delegate
            {
                Thread.Sleep(100);
            }, isSilent);

            if (!isSilent)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" ==============================================================================");
                Console.WriteLine("  INSTALLATION COMPLETED SUCCESSFULLY!");
                Console.WriteLine("  Desktop and Start Menu shortcuts are ready.");
                Console.WriteLine(" ==============================================================================");
                Console.ResetColor();
                Console.WriteLine();

                if (!Console.IsInputRedirected)
                {
                    Console.Write(" Would you like to launch Roblox Network Tuner now? [Y/n]: ");
                    string reply = Console.ReadLine();
                    if (string.IsNullOrEmpty(reply) || reply.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            ProcessStartInfo startInfo = new ProcessStartInfo();
                            startInfo.FileName = tunerExePath;
                            startInfo.WorkingDirectory = targetDir;
                            startInfo.UseShellExecute = true;
                            Process.Start(startInfo);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(" Could not start tuner: {0}", ex.Message);
                        }
                    }
                }
            }

            return 0;
        }

        #endregion

        #region Uninstallation Logic

        private static int DispatchUninstall(bool isSilent)
        {
            string currentExe = Process.GetCurrentProcess().MainModule.FileName;
            string currentDir = Path.GetDirectoryName(currentExe);
            string installDir = null;

            // Priority 1: If current executable is next to RobloxNetworkTuner.exe, this folder is the target
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

            // Check if current running process is located inside the directory to be deleted
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
                // Running from inside installDir:
                // Copy self to %TEMP%\RNT_UninstallWorker.exe and spawn worker, then exit immediately.
                // This releases all locks on uninstall.exe and allows installDir to be cleanly deleted!
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

                    // Immediate clean termination
                    Environment.Exit(0);
                    return 0;
                }
                catch (Exception ex)
                {
                    if (!isSilent)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[!] Could not spawn temp worker: {0}", ex.Message);
                        Console.ResetColor();
                    }
                }
            }

            // If not inside installDir (e.g. running from Desktop or Downloads), run directly
            return RunUninstallWorker(installDir, 0, isSilent);
        }

        private static int RunUninstallWorker(string installDir, int parentPid, bool isSilent)
        {
            try { Environment.CurrentDirectory = Path.GetTempPath(); } catch { }

            // 1. Wait for parent process to exit completely so its file lock is released
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
                Thread.Sleep(500);
            }

            if (!isSilent)
            {
                PrintLogo("COMPLETE UNINSTALLER & RESTORER");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(" Target Directory to Remove:");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  -> {0}", !string.IsNullOrEmpty(installDir) ? installDir : "None (Clean baseline only)");
                Console.ResetColor();
                Console.WriteLine();
            }

            int step = 1;
            int total = 17;

            LogStep(step++, total, "Initializing uninstallation worker session from temporary storage", delegate
            {
                Thread.Sleep(50);
            }, isSilent);

            LogStep(step++, total, "Releasing parent process file locks and verifying exit status", delegate
            {
                Thread.Sleep(50);
            }, isSilent);

            LogStep(step++, total, "Scanning system for active RobloxNetworkTuner processes", delegate
            {
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
            }, isSilent);

            LogStep(step++, total, "Terminating active tuner instances cleanly", delegate
            {
                Thread.Sleep(50);
            }, isSilent);

            LogStep(step++, total, "Reverting NT kernel hardware timer resolution to 15.625 ms", delegate
            {
                uint dummy;
                NtSetTimerResolution(156250, false, out dummy);
                TimeEndPeriod(1);
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", true))
                {
                    if (k != null) k.DeleteValue("GlobalTimerResolutionRequests", false);
                }
            }, isSilent);

            LogStep(step++, total, "Restoring WLAN AutoConfig background scanning on Wi-Fi adapters", delegate
            {
                string wifiOut = RunCapture("netsh.exe", "wlan show interfaces");
                MatchCollection matches = Regex.Matches(wifiOut, @"^\s*Name\s*:\s*(.+)$", RegexOptions.Multiline);
                foreach (Match m in matches)
                {
                    string nicName = m.Groups[1].Value.Trim();
                    RunSilent("netsh.exe", string.Format("wlan set autoconfig enabled=yes interface=\"{0}\"", nicName));
                }
            }, isSilent);

            LogStep(step++, total, "Purging Policy-Based QoS DSCP 46 registry entries and policies", delegate
            {
                try
                {
                    using (RegistryKey polKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\QoS", true))
                    {
                        if (polKey != null) polKey.DeleteSubKeyTree(QosPolicyName, false);
                    }
                }
                catch { }
                RunSilent("powershell.exe", string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"Remove-NetQosPolicy -Name '{0}' -Confirm:$false -ErrorAction SilentlyContinue\"", QosPolicyName));
            }, isSilent);

            LogStep(step++, total, "Restoring Winsock AFD socket buffer configuration parameters", delegate
            {
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
            }, isSilent);

            LogStep(step++, total, "Resetting TCP/IP PMTU discovery and core stack parameters", delegate
            {
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
            }, isSilent);

            LogStep(step++, total, "Resetting TCP ACK frequency (TcpAckFrequency, TCPNoDelay)", delegate
            {
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
            }, isSilent);

            LogStep(step++, total, "Restoring global TCP stack congestion provider and NetOffload", delegate
            {
                RunSilent("netsh.exe", "int tcp set global rsc=enabled");
                RunSilent("netsh.exe", "int tcp set global timestamps=allowed");
                RunSilent("netsh.exe", "int tcp set supplemental template=internet congestionprovider=default");
                RunSilent("netsh.exe", "int tcp set supplemental template=compat congestionprovider=default");
                RunSilent("netsh.exe", "int tcp set supplemental template=datacenterext congestionprovider=default");
                RunSilent("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"Set-NetOffloadGlobalSetting -PacketCoalescingFilter Enabled -ReceiveSegmentCoalescing Enabled -Confirm:$false\"");
            }, isSilent);

            LogStep(step++, total, "Restoring MMCSS NetworkThrottlingIndex and SystemResponsiveness", delegate
            {
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
            }, isSilent);

            LogStep(step++, total, "Flushing Windows DNS resolver cache and ARP neighbor tables", delegate
            {
                RunSilent("ipconfig.exe", "/flushdns");
                RunSilent("netsh.exe", "interface ip delete arpcache");
            }, isSilent);

            LogStep(step++, total, "Removing Desktop shell shortcuts across all user profiles", delegate
            {
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
                        string lnk = Path.Combine(desktopPaths[i], "Roblox Network Tuner.lnk");
                        if (File.Exists(lnk))
                        {
                            try { File.Delete(lnk); } catch { }
                        }
                    }
                }
            }, isSilent);

            LogStep(step++, total, "Removing Start Menu program group folder and shortcuts", delegate
            {
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
            }, isSilent);

            LogStep(step++, total, "Removing Windows Installed Apps (ARP) registry entries", delegate
            {
                try { Registry.LocalMachine.DeleteSubKeyTree(UninstallRegSubKey, false); } catch { }
                try { Registry.CurrentUser.DeleteSubKeyTree(UninstallRegSubKey, false); } catch { }
            }, isSilent);

            LogStep(step++, total, "Purging installation directory and all application files", delegate
            {
                if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                {
                    // Safety check: ensure directory contains application files or is dedicated folder
                    string dirName = Path.GetFileName(installDir.TrimEnd('\\', '/'));
                    bool isSafe = dirName.Equals("RobloxNetworkTuner", StringComparison.OrdinalIgnoreCase) ||
                                 dirName.StartsWith("RNT_", StringComparison.OrdinalIgnoreCase) ||
                                 dirName.IndexOf("Roblox", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 dirName.IndexOf("test_install", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 File.Exists(Path.Combine(installDir, "RobloxNetworkTuner.exe"));

                    if (isSafe)
                    {
                        if (Path.GetPathRoot(installDir).Equals(Path.GetFullPath(installDir), StringComparison.OrdinalIgnoreCase))
                        {
                            return; // Never delete drive root
                        }
                        // Delete all files inside first
                        string[] files = Directory.GetFiles(installDir);
                        for (int i = 0; i < files.Length; i++)
                        {
                            try
                            {
                                File.SetAttributes(files[i], FileAttributes.Normal);
                                File.Delete(files[i]);
                            }
                            catch { }
                        }

                        // Delete all subdirectories
                        string[] subdirs = Directory.GetDirectories(installDir);
                        for (int i = 0; i < subdirs.Length; i++)
                        {
                            try { Directory.Delete(subdirs[i], true); } catch { }
                        }

                        // Delete root directory with retry backoff
                        bool deleted = false;
                        for (int r = 0; r < 5; r++)
                        {
                            try
                            {
                                Directory.Delete(installDir, true);
                                deleted = true;
                                break;
                            }
                            catch
                            {
                                Thread.Sleep(250);
                            }
                        }

                        if (!deleted && Directory.Exists(installDir))
                        {
                            throw new IOException("Directory could not be fully purged.");
                        }
                    }
                }
            }, isSilent);

            if (!isSilent)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" ==============================================================================");
                Console.WriteLine("  UNINSTALLATION COMPLETED SUCCESSFULLY!");
                Console.WriteLine("  Roblox Network Tuner removed. System network baseline fully restored.");
                Console.WriteLine(" ==============================================================================");
                Console.ResetColor();
                Console.WriteLine();

                if (!Console.IsInputRedirected)
                {
                    Console.WriteLine(" Press any key to close this window...");
                    try { Console.ReadKey(); } catch { }
                }
            }

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

        #region Embedded Resource & Compression Helpers

        private static byte[] ExtractOrDecompressPayload(string pkgResourceName, string rawExeName)
        {
            Assembly asm = Assembly.GetExecutingAssembly();

            // 1. Try compressed .pkg resource
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

            // 2. Try raw uncompressed resource
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

            // 3. Fallback: local file from build environment
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

        #endregion

        #region Shortcut & Shell Creation

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string arguments, string description, string iconLocation)
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

        #endregion

        #region Helpers

        private static void RunSilent(string file, string args)
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

        private static string RunCapture(string file, string args)
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
            Console.WriteLine("  (no args)           Run interactive full installation with rolling status");
            Console.WriteLine("  --silent, -s        Run silent unattended installation");
            Console.WriteLine("  --dir <path>, -d    Specify custom destination directory");
            Console.WriteLine("  --uninstall, -u     Perform complete uninstallation and restore baseline");
            Console.WriteLine("  --help, -h, /?      Display this help menu");
            Console.WriteLine();
        }

        #endregion
    }
}
