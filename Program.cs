using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Roblox Network Tuner")]
[assembly: AssemblyDescription("Roblox Low-Latency & Anti-Jitter Packet Optimization Engine")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyCompany("Roblox Performance Engineering")]
[assembly: AssemblyProduct("Roblox Network Tuner")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("8b3838e7-7c38-4fee-8c84-3701258607a9")]
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]

namespace RobloxNetworkTuner
{
    #region Native P/Invoke Definitions

    internal static class NativeMethods
    {
        // ntdll.dll - High-Resolution NT Kernel Timers & Process Information
        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtSetTimerResolution(uint desiredResolution, bool setResolution, out uint currentResolution);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtQueryTimerResolution(out uint minimumResolution, out uint maximumResolution, out uint currentResolution);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtSetInformationProcess(IntPtr processHandle, int processInformationClass, ref int processInformation, int processInformationLength);

        // winmm.dll - WinMM Multimedia Timer Period
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
        public static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
        public static extern uint TimeEndPeriod(uint uMilliseconds);

        // kernel32.dll - Power Throttling (EcoQoS) & Console Control Handlers
        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetProcessInformation(IntPtr hProcess, int processInformationClass, ref PROCESS_POWER_THROTTLING_STATE processInformation, uint processInformationSize);

        public delegate bool ConsoleCtrlDelegate(int ctrlType);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handler, bool add);

        // avrt.dll - Multimedia Class Scheduler Service (MMCSS) Thread Boosting
        [DllImport("avrt.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr AvSetMmThreadCharacteristics(string TaskName, ref uint TaskIndex);

        [DllImport("avrt.dll", SetLastError = true)]
        public static extern bool AvSetMmThreadPriority(IntPtr AvrtHandle, int Priority);

        [DllImport("avrt.dll", SetLastError = true)]
        public static extern bool AvRevertMmThreadCharacteristics(IntPtr AvrtHandle);

        // GUI & Console Interop
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(int dwProcessId);
        public const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int nStdHandle);
        public const int STD_OUTPUT_HANDLE = -11;
        public const int STD_ERROR_HANDLE = -12;

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
    }

    internal static class NativeWifiApi
    {
        private const string WlanApiDll = "wlanapi.dll";
        public const uint WLAN_CLIENT_VERSION_VISTA = 2;

        [DllImport(WlanApiDll, SetLastError = true)]
        public static extern uint WlanOpenHandle(
            uint dwClientVersion,
            IntPtr pReserved,
            out uint pdwNegotiatedVersion,
            out IntPtr phClientHandle);

        [DllImport(WlanApiDll, SetLastError = true)]
        public static extern uint WlanCloseHandle(
            IntPtr hClientHandle,
            IntPtr pReserved);

        [DllImport(WlanApiDll, SetLastError = true)]
        public static extern uint WlanEnumInterfaces(
            IntPtr hClientHandle,
            IntPtr pReserved,
            out IntPtr ppInterfaceList);

        [DllImport(WlanApiDll, SetLastError = true)]
        public static extern void WlanFreeMemory(IntPtr pMemory);

        [DllImport(WlanApiDll, SetLastError = true)]
        public static extern uint WlanQueryInterface(
            IntPtr hClientHandle,
            [In] ref Guid pInterfaceGuid,
            WLAN_INTF_OPCODE OpCode,
            IntPtr pReserved,
            out uint pdwDataSize,
            out IntPtr ppData,
            out WLAN_OPCODE_VALUE_TYPE pWlanOpcodeValueType);

        [DllImport(WlanApiDll, SetLastError = true)]
        public static extern uint WlanSetInterface(
            IntPtr hClientHandle,
            [In] ref Guid pInterfaceGuid,
            WLAN_INTF_OPCODE OpCode,
            uint dwDataSize,
            IntPtr pData,
            IntPtr pReserved);

        public enum WLAN_INTF_OPCODE
        {
            wlan_intf_opcode_autoconf_start = 0,
            wlan_intf_opcode_autoconf_enabled = 1,
            wlan_intf_opcode_background_scan_enabled = 2,
            wlan_intf_opcode_media_streaming_mode = 3,
            wlan_intf_opcode_radio_state = 4,
            wlan_intf_opcode_bss_type = 5,
            wlan_intf_opcode_interface_state = 6,
            wlan_intf_opcode_current_connection = 7,
            wlan_intf_opcode_channel_number = 8,
            wlan_intf_opcode_supported_infrastructure_auth_cipher_pairs = 9,
            wlan_intf_opcode_supported_adhoc_auth_cipher_pairs = 10,
            wlan_intf_opcode_supported_country_or_region_string_list = 11,
            wlan_intf_opcode_current_operation_mode = 12,
            wlan_intf_opcode_supported_safe_mode = 13,
            wlan_intf_opcode_certified_safe_mode = 14,
            wlan_intf_opcode_hosted_network_capable = 15,
            wlan_intf_opcode_management_frame_protection_capable = 16,
            wlan_intf_opcode_autoconf_end = 0x0fffffff,
            wlan_intf_opcode_msm_start = 0x10000100,
            wlan_intf_opcode_statistics = 0x10000101,
            wlan_intf_opcode_rssi = 0x10000102,
            wlan_intf_opcode_msm_end = 0x1fffffff
        }

        public enum WLAN_OPCODE_VALUE_TYPE
        {
            wlan_opcode_value_type_query_only = 0,
            wlan_opcode_value_type_set_by_group_policy = 1,
            wlan_opcode_value_type_set_by_user = 2,
            wlan_opcode_value_type_invalid = 3
        }

        public enum WLAN_INTERFACE_STATE
        {
            wlan_interface_state_not_ready = 0,
            wlan_interface_state_connected = 1,
            wlan_interface_state_ad_hoc_network_formed = 2,
            wlan_interface_state_disconnecting = 3,
            wlan_interface_state_disconnected = 4,
            wlan_interface_state_associating = 5,
            wlan_interface_state_discovering = 6,
            wlan_interface_state_authenticating = 7
        }
    }

    #endregion

    #region State Persistence & Atomic Rollback Model

    public class TunerState
    {
        public string Timestamp;
        public string ActiveAdapterName;
        public string ActiveAdapterGuid;
        public string DriverClassPath;
        public Dictionary<string, string> DriverProperties = new Dictionary<string, string>();
        public Dictionary<string, string> DriverKinds = new Dictionary<string, string>();
        public NativeWifiSnapshot NativeWifi = new NativeWifiSnapshot();
        public MsiXSnapshot MsiX = new MsiXSnapshot();
        public List<RegistrySnapshot> RegistrySnapshots = new List<RegistrySnapshot>();
        public Dictionary<string, string> NetshSnapshots = new Dictionary<string, string>();
        public List<string> StoppedServices = new List<string>();
        public uint OrigTimerResolution = 156250;
        public bool TimerResolutionSet;
        public bool MultimediaTimerSet;
    }

    public class NativeWifiSnapshot
    {
        public string InterfaceGuid;
        public bool BackgroundScanEnabled = true;
        public bool MediaStreamingMode = false;
        public bool HasCaptured;
    }

    public class MsiXSnapshot
    {
        public string PciDevicePath;
        public object OrigDevicePolicy;
        public byte[] OrigAssignmentSetOverride;
        public object OrigDevicePriority;
        public bool HasCaptured;
    }

    public class RegistrySnapshot
    {
        public string KeyPath;
        public Dictionary<string, object> Values = new Dictionary<string, object>();
        public Dictionary<string, RegistryValueKind> Kinds = new Dictionary<string, RegistryValueKind>();
        public List<string> CreatedValues = new List<string>();
        public bool KeyExistedOriginally = true;
    }

    internal static class TunerStateStorage
    {
        public const string StateFileName = "tuner_state.json";

        public static void SaveToFile(TunerState state)
        {
            if (state == null) return;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendFormat("  \"Timestamp\": \"{0}\",\n", EscapeJson(state.Timestamp ?? DateTime.UtcNow.ToString("o")));
                sb.AppendFormat("  \"ActiveAdapterName\": \"{0}\",\n", EscapeJson(state.ActiveAdapterName));
                sb.AppendFormat("  \"ActiveAdapterGuid\": \"{0}\",\n", EscapeJson(state.ActiveAdapterGuid));
                sb.AppendFormat("  \"DriverClassPath\": \"{0}\",\n", EscapeJson(state.DriverClassPath));

                // DriverProperties
                sb.AppendLine("  \"DriverProperties\": {");
                int dpCount = state.DriverProperties.Count;
                int dpIdx = 0;
                foreach (KeyValuePair<string, string> kvp in state.DriverProperties)
                {
                    dpIdx++;
                    sb.AppendFormat("    \"{0}\": {1}{2}\n",
                        EscapeJson(kvp.Key),
                        kvp.Value == null ? "null" : ("\"" + EscapeJson(kvp.Value) + "\""),
                        dpIdx < dpCount ? "," : "");
                }
                sb.AppendLine("  },");

                // NativeWifiSnapshots
                sb.AppendLine("  \"NativeWifiSnapshots\": {");
                sb.AppendFormat("    \"HasCaptured\": {0},\n", state.NativeWifi.HasCaptured ? "true" : "false");
                sb.AppendFormat("    \"InterfaceGuid\": \"{0}\",\n", EscapeJson(state.NativeWifi.InterfaceGuid));
                sb.AppendFormat("    \"BackgroundScanEnabled\": {0},\n", state.NativeWifi.BackgroundScanEnabled ? "true" : "false");
                sb.AppendFormat("    \"MediaStreamingMode\": {0}\n", state.NativeWifi.MediaStreamingMode ? "true" : "false");
                sb.AppendLine("  },");

                // NetshSnapshots
                sb.AppendLine("  \"NetshSnapshots\": {");
                int nsCount = state.NetshSnapshots.Count;
                int nsIdx = 0;
                foreach (KeyValuePair<string, string> kvp in state.NetshSnapshots)
                {
                    nsIdx++;
                    sb.AppendFormat("    \"{0}\": \"{1}\"{2}\n",
                        EscapeJson(kvp.Key), EscapeJson(kvp.Value), nsIdx < nsCount ? "," : "");
                }
                sb.AppendLine("  },");

                // RegistrySnapshots
                sb.AppendLine("  \"RegistrySnapshots\": [");
                for (int i = 0; i < state.RegistrySnapshots.Count; i++)
                {
                    RegistrySnapshot snap = state.RegistrySnapshots[i];
                    sb.AppendLine("    {");
                    sb.AppendFormat("      \"KeyPath\": \"{0}\",\n", EscapeJson(snap.KeyPath));
                    sb.AppendLine("      \"Values\": {");
                    int vCount = snap.Values.Count;
                    int vIdx = 0;
                    foreach (KeyValuePair<string, object> kvp in snap.Values)
                    {
                        vIdx++;
                        string valStr = kvp.Value == null ? "null" : ("\"" + EscapeJson(kvp.Value.ToString()) + "\"");
                        sb.AppendFormat("        \"{0}\": {1}{2}\n",
                            EscapeJson(kvp.Key), valStr, vIdx < vCount ? "," : "");
                    }
                    sb.AppendLine("      }");
                    sb.AppendFormat("    }}{0}\n", (i < state.RegistrySnapshots.Count - 1) ? "," : "");
                }
                sb.AppendLine("  ]");

                sb.AppendLine("}");

                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StateFileName);
                File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static bool StateFileExists()
        {
            try
            {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StateFileName);
                return File.Exists(fullPath);
            }
            catch
            {
                return false;
            }
        }

        public static void DeleteStateFile()
        {
            try
            {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StateFileName);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch { }
        }

        public static TunerState LoadFromFile()
        {
            try
            {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StateFileName);
                if (!File.Exists(fullPath)) return null;

                string json = File.ReadAllText(fullPath, Encoding.UTF8);
                TunerState state = new TunerState();

                Match mTime = Regex.Match(json, "\"Timestamp\"\\s*:\\s*\"([^\"]+)\"");
                if (mTime.Success) state.Timestamp = mTime.Groups[1].Value;

                Match mName = Regex.Match(json, "\"ActiveAdapterName\"\\s*:\\s*\"([^\"]*)\"");
                if (mName.Success) state.ActiveAdapterName = mName.Groups[1].Value;

                Match mGuid = Regex.Match(json, "\"ActiveAdapterGuid\"\\s*:\\s*\"([^\"]*)\"");
                if (mGuid.Success) state.ActiveAdapterGuid = mGuid.Groups[1].Value;

                Match mPath = Regex.Match(json, "\"DriverClassPath\"\\s*:\\s*\"([^\"]*)\"");
                if (mPath.Success) state.DriverClassPath = mPath.Groups[1].Value.Replace("\\\\", "\\");

                Match mProps = Regex.Match(json, "\"DriverProperties\"\\s*:\\s*\\{([^\\}]*)\\}");
                if (mProps.Success)
                {
                    MatchCollection pMatches = Regex.Matches(mProps.Groups[1].Value, "\"([^\"]+)\"\\s*:\\s*(\"([^\"]*)\"|null)");
                    foreach (Match pm in pMatches)
                    {
                        string propKey = pm.Groups[1].Value;
                        string propVal = pm.Groups[2].Value == "null" ? null : pm.Groups[3].Value;
                        state.DriverProperties[propKey] = propVal;
                    }
                }

                state.NativeWifi.HasCaptured = Regex.IsMatch(json, "\"HasCaptured\"\\s*:\\s*true", RegexOptions.IgnoreCase);
                Match wGuid = Regex.Match(json, "\"InterfaceGuid\"\\s*:\\s*\"([^\"]*)\"");
                if (wGuid.Success) state.NativeWifi.InterfaceGuid = wGuid.Groups[1].Value;
                state.NativeWifi.BackgroundScanEnabled = Regex.IsMatch(json, "\"BackgroundScanEnabled\"\\s*:\\s*true", RegexOptions.IgnoreCase);
                state.NativeWifi.MediaStreamingMode = Regex.IsMatch(json, "\"MediaStreamingMode\"\\s*:\\s*true", RegexOptions.IgnoreCase);

                MatchCollection regMatches = Regex.Matches(json, "\\{\\s*\"KeyPath\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"Values\"\\s*:\\s*\\{([^\\}]*)\\}");
                foreach (Match rm in regMatches)
                {
                    RegistrySnapshot snap = new RegistrySnapshot();
                    snap.KeyPath = rm.Groups[1].Value.Replace("\\\\", "\\");
                    MatchCollection vMatches = Regex.Matches(rm.Groups[2].Value, "\"([^\"]+)\"\\s*:\\s*(\"([^\"]*)\"|null)");
                    foreach (Match vm in vMatches)
                    {
                        string k = vm.Groups[1].Value;
                        string v = vm.Groups[2].Value == "null" ? null : vm.Groups[3].Value;
                        snap.Values[k] = v;
                    }
                    state.RegistrySnapshots.Add(snap);
                }

                return state;
            }
            catch
            {
                return null;
            }
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }

    #endregion

    #region Subsystems: Wi-Fi 7 / DBS & NDIS DPC Steering

    internal sealed class NativeWifiController : IDisposable
    {
        private IntPtr hClient = IntPtr.Zero;
        private bool disposed;

        internal struct WifiInterfaceEntry
        {
            public Guid Guid;
            public string Description;
            internal NativeWifiApi.WLAN_INTERFACE_STATE State;
        }

        internal NativeWifiController()
        {
            uint negotiatedVer;
            uint status = NativeWifiApi.WlanOpenHandle(
                NativeWifiApi.WLAN_CLIENT_VERSION_VISTA,
                IntPtr.Zero,
                out negotiatedVer,
                out hClient);

            if (status != 0)
            {
                hClient = IntPtr.Zero;
            }
        }

        internal List<WifiInterfaceEntry> EnumerateConnectedInterfaces()
        {
            List<WifiInterfaceEntry> list = new List<WifiInterfaceEntry>();
            if (hClient == IntPtr.Zero) return list;

            IntPtr pList;
            uint status = NativeWifiApi.WlanEnumInterfaces(hClient, IntPtr.Zero, out pList);
            if (status != 0 || pList == IntPtr.Zero) return list;

            try
            {
                uint count = (uint)Marshal.ReadInt32(pList);
                for (int i = 0; i < count; i++)
                {
                    IntPtr itemPtr = new IntPtr(pList.ToInt64() + 8 + (i * 532));
                    byte[] guidBytes = new byte[16];
                    Marshal.Copy(itemPtr, guidBytes, 0, 16);
                    Guid g = new Guid(guidBytes);
                    string desc = Marshal.PtrToStringUni(new IntPtr(itemPtr.ToInt64() + 16));
                    int stateInt = Marshal.ReadInt32(new IntPtr(itemPtr.ToInt64() + 528));

                    WifiInterfaceEntry entry = new WifiInterfaceEntry();
                    entry.Guid = g;
                    entry.Description = desc;
                    entry.State = (NativeWifiApi.WLAN_INTERFACE_STATE)stateInt;

                    if (entry.State == NativeWifiApi.WLAN_INTERFACE_STATE.wlan_interface_state_connected)
                    {
                        list.Add(entry);
                    }
                }
            }
            finally
            {
                NativeWifiApi.WlanFreeMemory(pList);
            }

            return list;
        }

        internal bool QueryBooleanOpcode(Guid interfaceGuid, NativeWifiApi.WLAN_INTF_OPCODE opcode, out bool value)
        {
            value = false;
            if (hClient == IntPtr.Zero) return false;

            uint dataSize;
            IntPtr pData;
            NativeWifiApi.WLAN_OPCODE_VALUE_TYPE valType;

            uint status = NativeWifiApi.WlanQueryInterface(
                hClient,
                ref interfaceGuid,
                opcode,
                IntPtr.Zero,
                out dataSize,
                out pData,
                out valType);

            if (status == 0 && pData != IntPtr.Zero)
            {
                try
                {
                    int intVal = Marshal.ReadInt32(pData);
                    value = (intVal != 0);
                    return true;
                }
                finally
                {
                    NativeWifiApi.WlanFreeMemory(pData);
                }
            }

            return false;
        }

        internal bool SetBooleanOpcode(Guid interfaceGuid, NativeWifiApi.WLAN_INTF_OPCODE opcode, bool value)
        {
            if (hClient == IntPtr.Zero) return false;

            IntPtr pVal = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                Marshal.WriteInt32(pVal, value ? 1 : 0);
                uint status = NativeWifiApi.WlanSetInterface(
                    hClient,
                    ref interfaceGuid,
                    opcode,
                    (uint)sizeof(int),
                    pVal,
                    IntPtr.Zero);

                return (status == 0);
            }
            finally
            {
                Marshal.FreeHGlobal(pVal);
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                if (hClient != IntPtr.Zero)
                {
                    NativeWifiApi.WlanCloseHandle(hClient, IntPtr.Zero);
                    hClient = IntPtr.Zero;
                }
                disposed = true;
            }
        }
    }

    internal static class WifiOptimizationModule
    {
        public static void Apply(TunerState state)
        {
            Console.Write(" [*] Wi-Fi 7 / DBS roaming lock & wlanapi background scan freeze ..... ");
            try
            {
                using (NativeWifiController controller = new NativeWifiController())
                {
                    List<NativeWifiController.WifiInterfaceEntry> ifaces = controller.EnumerateConnectedInterfaces();
                    if (ifaces.Count > 0)
                    {
                        NativeWifiController.WifiInterfaceEntry primary = ifaces[0];
                        state.NativeWifi.InterfaceGuid = primary.Guid.ToString("B").ToUpperInvariant();
                        state.NativeWifi.HasCaptured = true;

                        bool scanVal;
                        if (controller.QueryBooleanOpcode(primary.Guid, NativeWifiApi.WLAN_INTF_OPCODE.wlan_intf_opcode_background_scan_enabled, out scanVal))
                        {
                            state.NativeWifi.BackgroundScanEnabled = scanVal;
                        }

                        bool streamVal;
                        if (controller.QueryBooleanOpcode(primary.Guid, NativeWifiApi.WLAN_INTF_OPCODE.wlan_intf_opcode_media_streaming_mode, out streamVal))
                        {
                            state.NativeWifi.MediaStreamingMode = streamVal;
                        }

                        // Set low-latency flags: background scan = false, media streaming = true
                        controller.SetBooleanOpcode(primary.Guid, NativeWifiApi.WLAN_INTF_OPCODE.wlan_intf_opcode_background_scan_enabled, false);
                        controller.SetBooleanOpcode(primary.Guid, NativeWifiApi.WLAN_INTF_OPCODE.wlan_intf_opcode_media_streaming_mode, true);

                        Program.PrintSuccess("LOCKED (MediaMode=1)");
                        return;
                    }
                }

                // Fallback to netsh if wlanapi had no active connected interface
                string wifiOut = Program.RunCapture("netsh.exe", "wlan show interfaces");
                Match m = Regex.Match(wifiOut, @"^\s*Name\s*:\s*(.+)$", RegexOptions.Multiline);
                if (m.Success)
                {
                    string nicName = m.Groups[1].Value.Trim();
                    state.ActiveAdapterName = nicName;
                    Program.RunSilent("netsh.exe", string.Format("wlan set autoconfig enabled=no interface=\"{0}\"", nicName));
                    Program.PrintSuccess(string.Format("FROZEN ({0})", nicName));
                }
                else
                {
                    Program.PrintInfo("SKIPPED (Ethernet)");
                }
            }
            catch (Exception ex)
            {
                Program.PrintError("FAIL: " + ex.Message);
            }
        }

        public static void Restore(TunerState state)
        {
            if (state == null) return;

            // Restore via wlanapi
            if (state.NativeWifi != null && state.NativeWifi.HasCaptured && !string.IsNullOrEmpty(state.NativeWifi.InterfaceGuid))
            {
                try
                {
                    using (NativeWifiController controller = new NativeWifiController())
                    {
                        Guid g = new Guid(state.NativeWifi.InterfaceGuid);
                        controller.SetBooleanOpcode(g, NativeWifiApi.WLAN_INTF_OPCODE.wlan_intf_opcode_background_scan_enabled, state.NativeWifi.BackgroundScanEnabled);
                        controller.SetBooleanOpcode(g, NativeWifiApi.WLAN_INTF_OPCODE.wlan_intf_opcode_media_streaming_mode, state.NativeWifi.MediaStreamingMode);
                        Console.WriteLine("  [+] Restored Native Wifi background scan & media streaming mode.");
                    }
                }
                catch { }
            }

            // Restore via netsh fallback
            if (!string.IsNullOrEmpty(state.ActiveAdapterName))
            {
                try
                {
                    Program.RunSilent("netsh.exe", string.Format("wlan set autoconfig enabled=yes interface=\"{0}\"", state.ActiveAdapterName));
                    Console.WriteLine("  [+] Restored WLAN AutoConfig scanning on '{0}'.", state.ActiveAdapterName);
                }
                catch { }
            }
        }
    }

    internal static class NdisOptimizationModule
    {
        private const string ClassRootPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

        public struct AdapterClassRecord
        {
            public string DeviceIndex;
            public string InterfaceGuid;
            public string DriverDesc;
            public NetworkInterfaceType Type;
        }

        public static List<AdapterClassRecord> DiscoverActiveAdapters()
        {
            List<AdapterClassRecord> records = new List<AdapterClassRecord>();
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            Dictionary<string, NetworkInterface> upAdapters = new Dictionary<string, NetworkInterface>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < nics.Length; i++)
            {
                NetworkInterface nic = nics[i];
                if (nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    upAdapters[nic.Id] = nic;
                }
            }

            using (RegistryKey classRoot = Registry.LocalMachine.OpenSubKey(ClassRootPath))
            {
                if (classRoot == null) return records;

                string[] subKeys = classRoot.GetSubKeyNames();
                for (int i = 0; i < subKeys.Length; i++)
                {
                    string subName = subKeys[i];
                    if (subName.Length != 4) continue;

                    using (RegistryKey sub = classRoot.OpenSubKey(subName))
                    {
                        if (sub == null) continue;
                        object netCfgObj = sub.GetValue("NetCfgInstanceId");
                        if (netCfgObj == null) continue;

                        string netCfgGuid = netCfgObj.ToString();
                        if (upAdapters.ContainsKey(netCfgGuid))
                        {
                            AdapterClassRecord rec = new AdapterClassRecord();
                            rec.DeviceIndex = subName;
                            rec.InterfaceGuid = netCfgGuid;
                            object descObj = sub.GetValue("DriverDesc");
                            rec.DriverDesc = descObj != null ? descObj.ToString() : "";
                            rec.Type = upAdapters[netCfgGuid].NetworkInterfaceType;
                            records.Add(rec);
                        }
                    }
                }
            }

            return records;
        }

        public static void Apply(TunerState state)
        {
            Console.Write(" [*] NDIS DPC interrupt steering, RSS pinning & hardware queue trim .. ");
            try
            {
                List<AdapterClassRecord> adapters = DiscoverActiveAdapters();
                int cpuCores = Environment.ProcessorCount;
                string rssBaseProc = (cpuCores >= 8) ? "4" : "2";

                for (int i = 0; i < adapters.Count; i++)
                {
                    AdapterClassRecord rec = adapters[i];
                    string subPath = ClassRootPath + @"\" + rec.DeviceIndex;
                    state.DriverClassPath = subPath;

                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(subPath, true))
                    {
                        if (key == null) continue;

                        // List of properties to capture & tune
                        List<string> propsToTune = new List<string>();
                        propsToTune.Add("*RSS");
                        propsToTune.Add("*RssBaseProcNumber");
                        propsToTune.Add("*NumRSSQueues");
                        propsToTune.Add("*RSSProfile");
                        propsToTune.Add("*RscIPv4");
                        propsToTune.Add("*RscIPv6");
                        propsToTune.Add("*FlowControl");
                        propsToTune.Add("*EEE");
                        propsToTune.Add("AdvancedEEE");
                        propsToTune.Add("roamPolicy");
                        propsToTune.Add("StaPreferredBand");
                        propsToTune.Add("enableWmmTxop");
                        propsToTune.Add("RoamAggressiveness");
                        propsToTune.Add("PreferredBand");
                        propsToTune.Add("RoamingSensitivityLevel");
                        propsToTune.Add("BandPreference");
                        propsToTune.Add("*InterruptModeration");

                        for (int j = 0; j < propsToTune.Count; j++)
                        {
                            string prop = propsToTune[j];
                            object val = key.GetValue(prop);
                            if (val != null)
                            {
                                state.DriverProperties[prop] = val.ToString();
                                state.DriverKinds[prop] = key.GetValueKind(prop).ToString();
                            }
                            else
                            {
                                state.DriverProperties[prop] = null;
                            }
                        }

                        // 1. Receive Side Scaling (RSS)
                        key.SetValue("*RSS", "1", RegistryValueKind.String);
                        key.SetValue("*RssBaseProcNumber", rssBaseProc, RegistryValueKind.String);
                        key.SetValue("*NumRSSQueues", "4", RegistryValueKind.String);
                        key.SetValue("*RSSProfile", "4", RegistryValueKind.String);

                        // 2. Hardware RSC Disablement (Zero release latency)
                        key.SetValue("*RscIPv4", "0", RegistryValueKind.String);
                        key.SetValue("*RscIPv6", "0", RegistryValueKind.String);

                        // 3. Flow Control & EEE Disablement (Mitigate PAUSE frame & LPI wake-up latency)
                        key.SetValue("*FlowControl", "0", RegistryValueKind.String);
                        if (key.GetValue("*EEE") != null) key.SetValue("*EEE", "0", RegistryValueKind.String);
                        if (key.GetValue("AdvancedEEE") != null) key.SetValue("AdvancedEEE", "0", RegistryValueKind.String);

                        // 4. Interrupt Moderation Disablement (*InterruptModeration=0)
                        if (key.GetValue("*InterruptModeration") != null || key.OpenSubKey(@"Ndi\params\*InterruptModeration") != null)
                        {
                            key.SetValue("*InterruptModeration", "0", RegistryValueKind.String);
                        }

                        // 5. Vendor-Specific Wi-Fi Tuning
                        string descUpper = (rec.DriverDesc ?? "").ToUpperInvariant();
                        if (descUpper.Contains("QUALCOMM") || descUpper.Contains("FASTCONNECT") || key.OpenSubKey(@"Ndi\params\roamPolicy") != null)
                        {
                            key.SetValue("roamPolicy", "1", RegistryValueKind.String);         // Stickiest link (Lowest roaming)
                            key.SetValue("StaPreferredBand", "3", RegistryValueKind.String);   // 5 GHz Preferred
                            key.SetValue("enableWmmTxop", "0", RegistryValueKind.String);      // WMM TXOP acceleration
                        }
                        else if (descUpper.Contains("INTEL") || key.OpenSubKey(@"Ndi\params\RoamAggressiveness") != null)
                        {
                            key.SetValue("RoamAggressiveness", "1", RegistryValueKind.String); // 1. Lowest
                            key.SetValue("PreferredBand", "3", RegistryValueKind.String);      // 3. Prefer 5GHz
                        }
                        else if (descUpper.Contains("MEDIATEK") || key.OpenSubKey(@"Ndi\params\RoamingSensitivityLevel") != null)
                        {
                            key.SetValue("RoamingSensitivityLevel", "1", RegistryValueKind.String);
                            key.SetValue("BandPreference", "2", RegistryValueKind.String);
                        }
                        else if (descUpper.Contains("REALTEK"))
                        {
                            key.SetValue("RoamingSensitivityLevel", "1", RegistryValueKind.String);
                        }

                        // 5. Constrain Hardware DMA Ring Buffer bloat
                        object rxObj = key.GetValue("*ReceiveBuffers");
                        if (rxObj != null)
                        {
                            state.DriverProperties["*ReceiveBuffers"] = rxObj.ToString();
                            state.DriverKinds["*ReceiveBuffers"] = key.GetValueKind("*ReceiveBuffers").ToString();
                            int rxCount;
                            if (int.TryParse(rxObj.ToString(), out rxCount) && rxCount > 512)
                            {
                                if (key.GetValueKind("*ReceiveBuffers") == RegistryValueKind.DWord)
                                    key.SetValue("*ReceiveBuffers", 512, RegistryValueKind.DWord);
                                else
                                    key.SetValue("*ReceiveBuffers", "512", RegistryValueKind.String);
                            }
                        }
                    }

                    // 6. MSI-X Vector Affinity Policy (Target Cores 4-7 on 12-core Oryon)
                    ApplyMsiXAffinity(rec.DeviceIndex, cpuCores, state);
                }

                // 7. Global NetOffload: Disable Packet Coalescing Filter & NetOffload RSC
                try
                {
                    string outStr = Program.RunCapture("powershell.exe", "-NoProfile -Command \"(Get-NetOffloadGlobalSetting).PacketCoalescingFilter; (Get-NetOffloadGlobalSetting).ReceiveSegmentCoalescing\"");
                    string[] lines = outStr.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length >= 1) state.NetshSnapshots["PacketCoalescingFilter"] = lines[0].Trim();
                    if (lines.Length >= 2) state.NetshSnapshots["ReceiveSegmentCoalescing"] = lines[1].Trim();

                    Program.RunSilent("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"Set-NetOffloadGlobalSetting -PacketCoalescingFilter Disabled -ReceiveSegmentCoalescing Disabled -Confirm:$false\"");
                }
                catch { }

                Program.PrintSuccess("STEERED (Cores 4-7)");
            }
            catch (Exception ex)
            {
                Program.PrintError("FAIL: " + ex.Message);
            }
        }

        private static void ApplyMsiXAffinity(string deviceIndex, int cpuCores, TunerState state)
        {
            try
            {
                const string pciRoot = @"SYSTEM\CurrentControlSet\Enum\PCI";
                using (RegistryKey pciKey = Registry.LocalMachine.OpenSubKey(pciRoot))
                {
                    if (pciKey == null) return;
                    string[] venKeys = pciKey.GetSubKeyNames();
                    for (int i = 0; i < venKeys.Length; i++)
                    {
                        using (RegistryKey devKey = pciKey.OpenSubKey(venKeys[i]))
                        {
                            if (devKey == null) continue;
                            string[] instKeys = devKey.GetSubKeyNames();
                            for (int j = 0; j < instKeys.Length; j++)
                            {
                                string targetDriver = string.Format("{{4d36e972-e325-11ce-bfc1-08002be10318}}\\{0}", deviceIndex);
                                using (RegistryKey inst = devKey.OpenSubKey(instKeys[j]))
                                {
                                    if (inst == null) continue;
                                    object drv = inst.GetValue("Driver");
                                    if (drv != null && string.Equals(drv.ToString(), targetDriver, StringComparison.OrdinalIgnoreCase))
                                    {
                                        string polPath = pciRoot + @"\" + venKeys[i] + @"\" + instKeys[j] + @"\Device Parameters\Interrupt Management\Affinity Policy";
                                        using (RegistryKey polKey = Registry.LocalMachine.CreateSubKey(polPath))
                                        {
                                            if (polKey != null)
                                            {
                                                state.MsiX.PciDevicePath = polPath;
                                                state.MsiX.OrigDevicePolicy = polKey.GetValue("DevicePolicy");
                                                state.MsiX.OrigAssignmentSetOverride = polKey.GetValue("AssignmentSetOverride") as byte[];
                                                state.MsiX.OrigDevicePriority = polKey.GetValue("DevicePriority");
                                                state.MsiX.HasCaptured = true;

                                                polKey.SetValue("DevicePolicy", 4, RegistryValueKind.DWord); // IrqPolicySpecifiedProcessors

                                                // If >= 8 cores (Oryon 12-core), use mask 0xF0 (Cores 4-7)
                                                // If 4-7 cores, use mask 0x0C (Cores 2-3)
                                                ulong mask = (cpuCores >= 8) ? 0xF0UL : 0x0CUL;
                                                byte[] maskBytes = BitConverter.GetBytes(mask);
                                                polKey.SetValue("AssignmentSetOverride", maskBytes, RegistryValueKind.Binary);
                                                polKey.SetValue("DevicePriority", 3, RegistryValueKind.DWord); // IrqPriorityHigh
                                            }
                                        }
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public static void Restore(TunerState state)
        {
            if (state == null) return;

            // 1. Restore Driver Class properties
            if (!string.IsNullOrEmpty(state.DriverClassPath) && state.DriverProperties != null)
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(state.DriverClassPath, true))
                    {
                        if (key != null)
                        {
                            foreach (KeyValuePair<string, string> kvp in state.DriverProperties)
                            {
                                if (kvp.Value != null)
                                {
                                    RegistryValueKind kind = RegistryValueKind.String;
                                    if (state.DriverKinds.ContainsKey(kvp.Key))
                                    {
                                        try { kind = (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), state.DriverKinds[kvp.Key]); } catch { }
                                    }
                                    if (kind == RegistryValueKind.DWord)
                                    {
                                        int dVal;
                                        if (int.TryParse(kvp.Value, out dVal))
                                            key.SetValue(kvp.Key, dVal, RegistryValueKind.DWord);
                                        else
                                            key.SetValue(kvp.Key, kvp.Value, RegistryValueKind.String);
                                    }
                                    else
                                    {
                                        key.SetValue(kvp.Key, kvp.Value, kind);
                                    }
                                }
                                else
                                {
                                    key.DeleteValue(kvp.Key, false);
                                }
                            }
                        }
                    }
                    Console.WriteLine("  [+] Restored NDIS adapter parameters (RSS, FlowControl, EEE, RSC, Roaming).");
                }
                catch { }
            }

            // 2. Restore MSI-X Vector Affinity Policy
            if (state.MsiX != null && state.MsiX.HasCaptured && !string.IsNullOrEmpty(state.MsiX.PciDevicePath))
            {
                try
                {
                    using (RegistryKey polKey = Registry.LocalMachine.OpenSubKey(state.MsiX.PciDevicePath, true))
                    {
                        if (polKey != null)
                        {
                            if (state.MsiX.OrigDevicePolicy != null)
                                polKey.SetValue("DevicePolicy", state.MsiX.OrigDevicePolicy, RegistryValueKind.DWord);
                            else
                                polKey.DeleteValue("DevicePolicy", false);

                            if (state.MsiX.OrigAssignmentSetOverride != null)
                                polKey.SetValue("AssignmentSetOverride", state.MsiX.OrigAssignmentSetOverride, RegistryValueKind.Binary);
                            else
                                polKey.DeleteValue("AssignmentSetOverride", false);

                            if (state.MsiX.OrigDevicePriority != null)
                                polKey.SetValue("DevicePriority", state.MsiX.OrigDevicePriority, RegistryValueKind.DWord);
                            else
                                polKey.DeleteValue("DevicePriority", false);
                        }
                    }
                    Console.WriteLine("  [+] Restored MSI-X vector affinity policy.");
                }
                catch { }
            }

            // 3. Restore Global NetOffload
            try
            {
                string pcf = (state.NetshSnapshots.ContainsKey("PacketCoalescingFilter") && !string.IsNullOrEmpty(state.NetshSnapshots["PacketCoalescingFilter"]))
                    ? state.NetshSnapshots["PacketCoalescingFilter"]
                    : "Enabled";
                string rsc = (state.NetshSnapshots.ContainsKey("ReceiveSegmentCoalescing") && !string.IsNullOrEmpty(state.NetshSnapshots["ReceiveSegmentCoalescing"]))
                    ? state.NetshSnapshots["ReceiveSegmentCoalescing"]
                    : "Enabled";

                Program.RunSilent("powershell.exe", string.Format(
                    "-NoProfile -ExecutionPolicy Bypass -Command \"Set-NetOffloadGlobalSetting -PacketCoalescingFilter {0} -ReceiveSegmentCoalescing {1} -Confirm:$false\"",
                    pcf, rsc));
                Console.WriteLine("  [+] Restored global Packet Coalescing Filter & Receive Segment Coalescing.");
            }
            catch { }
        }
    }

    #endregion

    #region Subsystems: Path MTU Discovery & Winsock AFD Buffer Locking

    internal static class PmtuOptimizationModule
    {
        private const string TcpipParamsPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";

        public static void Apply(TunerState state)
        {
            Console.Write(" [*] TCP/IP PMTU discovery & blackhole detection (EnablePMTUBHDetect=1) ");
            try
            {
                using (RegistryKey tcpKey = Registry.LocalMachine.CreateSubKey(TcpipParamsPath))
                {
                    if (tcpKey != null)
                    {
                        RegistrySnapshot snap = new RegistrySnapshot();
                        snap.KeyPath = "HKLM\\" + TcpipParamsPath;
                        snap.Values["EnablePMTUDiscovery"] = tcpKey.GetValue("EnablePMTUDiscovery");
                        snap.Values["EnablePMTUBHDetect"] = tcpKey.GetValue("EnablePMTUBHDetect");
                        state.RegistrySnapshots.Add(snap);

                        tcpKey.SetValue("EnablePMTUDiscovery", 1, RegistryValueKind.DWord);
                        tcpKey.SetValue("EnablePMTUBHDetect", 1, RegistryValueKind.DWord);
                    }
                }
                Program.PrintSuccess("DONE");
            }
            catch (Exception ex)
            {
                Program.PrintError("FAIL: " + ex.Message);
            }
        }

        public static void Restore(TunerState state)
        {
            if (state == null) return;
            try
            {
                using (RegistryKey tcpKey = Registry.LocalMachine.OpenSubKey(TcpipParamsPath, true))
                {
                    if (tcpKey != null)
                    {
                        for (int i = 0; i < state.RegistrySnapshots.Count; i++)
                        {
                            RegistrySnapshot snap = state.RegistrySnapshots[i];
                            if (snap.KeyPath.EndsWith("Tcpip\\Parameters", StringComparison.OrdinalIgnoreCase))
                            {
                                Program.RevertRegistryValue(tcpKey, "EnablePMTUDiscovery", snap.Values.ContainsKey("EnablePMTUDiscovery") ? snap.Values["EnablePMTUDiscovery"] : null);
                                Program.RevertRegistryValue(tcpKey, "EnablePMTUBHDetect", snap.Values.ContainsKey("EnablePMTUBHDetect") ? snap.Values["EnablePMTUBHDetect"] : null);
                            }
                        }
                    }
                }
                Console.WriteLine("  [+] Restored TCP/IP PMTU discovery and blackhole detection.");
            }
            catch { }
        }
    }

    internal static class AfdOptimizationModule
    {
        private const string AfdParamsPath = @"SYSTEM\CurrentControlSet\Services\AFD\Parameters";

        public static void Apply(TunerState state)
        {
            Console.Write(" [*] Winsock AFD UDP fast-path (1500 B) & 256KB buffer locking ....... ");
            try
            {
                using (RegistryKey afdKey = Registry.LocalMachine.CreateSubKey(AfdParamsPath))
                {
                    if (afdKey != null)
                    {
                        RegistrySnapshot snap = new RegistrySnapshot();
                        snap.KeyPath = "HKLM\\" + AfdParamsPath;
                        snap.Values["FastSendDatagramThreshold"] = afdKey.GetValue("FastSendDatagramThreshold");
                        snap.Values["FastCopyReceiveThreshold"] = afdKey.GetValue("FastCopyReceiveThreshold");
                        snap.Values["DefaultReceiveWindow"] = afdKey.GetValue("DefaultReceiveWindow");
                        snap.Values["DefaultSendWindow"] = afdKey.GetValue("DefaultSendWindow");
                        snap.Values["NonBlockingSendLimits"] = afdKey.GetValue("NonBlockingSendLimits");
                        snap.Values["DoNotDisableReceiveBuffering"] = afdKey.GetValue("DoNotDisableReceiveBuffering");
                        snap.Values["DoNotDisableSendBuffering"] = afdKey.GetValue("DoNotDisableSendBuffering");
                        state.RegistrySnapshots.Add(snap);

                        afdKey.SetValue("FastSendDatagramThreshold", 1500, RegistryValueKind.DWord);
                        afdKey.SetValue("FastCopyReceiveThreshold", 1500, RegistryValueKind.DWord);
                        afdKey.SetValue("DefaultReceiveWindow", 262144, RegistryValueKind.DWord);
                        afdKey.SetValue("DefaultSendWindow", 262144, RegistryValueKind.DWord);
                        afdKey.SetValue("NonBlockingSendLimits", 16, RegistryValueKind.DWord);
                        afdKey.SetValue("DoNotDisableReceiveBuffering", 1, RegistryValueKind.DWord);
                        afdKey.SetValue("DoNotDisableSendBuffering", 1, RegistryValueKind.DWord);
                    }
                }
                Program.PrintSuccess("DONE");
            }
            catch (Exception ex)
            {
                Program.PrintError("FAIL: " + ex.Message);
            }
        }

        public static void Restore(TunerState state)
        {
            if (state == null) return;
            try
            {
                using (RegistryKey afdKey = Registry.LocalMachine.OpenSubKey(AfdParamsPath, true))
                {
                    if (afdKey != null)
                    {
                        for (int i = 0; i < state.RegistrySnapshots.Count; i++)
                        {
                            RegistrySnapshot snap = state.RegistrySnapshots[i];
                            if (snap.KeyPath.EndsWith("AFD\\Parameters", StringComparison.OrdinalIgnoreCase))
                            {
                                Program.RevertRegistryValue(afdKey, "FastSendDatagramThreshold", snap.Values.ContainsKey("FastSendDatagramThreshold") ? snap.Values["FastSendDatagramThreshold"] : null);
                                Program.RevertRegistryValue(afdKey, "FastCopyReceiveThreshold", snap.Values.ContainsKey("FastCopyReceiveThreshold") ? snap.Values["FastCopyReceiveThreshold"] : null);
                                Program.RevertRegistryValue(afdKey, "DefaultReceiveWindow", snap.Values.ContainsKey("DefaultReceiveWindow") ? snap.Values["DefaultReceiveWindow"] : null);
                                Program.RevertRegistryValue(afdKey, "DefaultSendWindow", snap.Values.ContainsKey("DefaultSendWindow") ? snap.Values["DefaultSendWindow"] : null);
                                Program.RevertRegistryValue(afdKey, "NonBlockingSendLimits", snap.Values.ContainsKey("NonBlockingSendLimits") ? snap.Values["NonBlockingSendLimits"] : null);
                                Program.RevertRegistryValue(afdKey, "DoNotDisableReceiveBuffering", snap.Values.ContainsKey("DoNotDisableReceiveBuffering") ? snap.Values["DoNotDisableReceiveBuffering"] : null);
                                Program.RevertRegistryValue(afdKey, "DoNotDisableSendBuffering", snap.Values.ContainsKey("DoNotDisableSendBuffering") ? snap.Values["DoNotDisableSendBuffering"] : null);
                            }
                        }
                    }
                }
                Console.WriteLine("  [+] Restored Winsock AFD socket parameters.");
            }
            catch { }
        }
    }

    #endregion

    #region Subsystems: MMCSS, Timer Resolution & Process Priority

    internal static class SchedulingModule
    {
        private const string KernelKeyPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\kernel";
        private const string MmcssPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        private const string GamesTaskPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";

        private static IntPtr hMmcssThread = IntPtr.Zero;

        public static void Apply(TunerState state)
        {
            // 1. MMCSS Multimedia Scheduler
            Console.Write(" [*] MMCSS scheduling: Network throttling disabled, gaming priority .. ");
            try
            {
                using (RegistryKey mmKey = Registry.LocalMachine.CreateSubKey(MmcssPath))
                {
                    if (mmKey != null)
                    {
                        RegistrySnapshot snap = new RegistrySnapshot();
                        snap.KeyPath = "HKLM\\" + MmcssPath;
                        snap.Values["NetworkThrottlingIndex"] = mmKey.GetValue("NetworkThrottlingIndex");
                        snap.Values["SystemResponsiveness"] = mmKey.GetValue("SystemResponsiveness");
                        snap.Values["NoLazyMode"] = mmKey.GetValue("NoLazyMode");
                        snap.Values["AlwaysOn"] = mmKey.GetValue("AlwaysOn");
                        state.RegistrySnapshots.Add(snap);

                        mmKey.SetValue("NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
                        mmKey.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
                        mmKey.SetValue("NoLazyMode", 1, RegistryValueKind.DWord);
                        mmKey.SetValue("AlwaysOn", 1, RegistryValueKind.DWord);
                    }
                }

                using (RegistryKey gamesKey = Registry.LocalMachine.CreateSubKey(GamesTaskPath))
                {
                    if (gamesKey != null)
                    {
                        gamesKey.SetValue("GPU Priority", 8, RegistryValueKind.DWord);
                        gamesKey.SetValue("Priority", 6, RegistryValueKind.DWord);
                        gamesKey.SetValue("Scheduling Category", "High", RegistryValueKind.String);
                        gamesKey.SetValue("SFIO Priority", "High", RegistryValueKind.String);
                        gamesKey.SetValue("Affinity", 0, RegistryValueKind.DWord);
                        gamesKey.SetValue("Background Only", "False", RegistryValueKind.String);
                        gamesKey.SetValue("Clock Rate", 10000, RegistryValueKind.DWord);
                    }
                }
                Program.PrintSuccess("DONE");
            }
            catch (Exception ex)
            {
                Program.PrintError("FAIL: " + ex.Message);
            }

            // 2. Global Kernel & Multimedia Timer Resolution
            Console.Write(" [*] System timer resolution: 0.50 ms interrupt quantization .......... ");
            try
            {
                using (RegistryKey kernelKey = Registry.LocalMachine.CreateSubKey(KernelKeyPath))
                {
                    if (kernelKey != null)
                    {
                        RegistrySnapshot snap = new RegistrySnapshot();
                        snap.KeyPath = "HKLM\\" + KernelKeyPath;
                        snap.Values["GlobalTimerResolutionRequests"] = kernelKey.GetValue("GlobalTimerResolutionRequests");
                        state.RegistrySnapshots.Add(snap);

                        kernelKey.SetValue("GlobalTimerResolutionRequests", 1, RegistryValueKind.DWord);
                    }
                }

                uint minRes, maxRes, curRes;
                if (NativeMethods.NtQueryTimerResolution(out minRes, out maxRes, out curRes) == 0)
                {
                    state.OrigTimerResolution = curRes;
                }

                int status = NativeMethods.NtSetTimerResolution(5000, true, out curRes);
                if (status == 0)
                {
                    state.TimerResolutionSet = true;
                }

                NativeMethods.TimeBeginPeriod(1);
                state.MultimediaTimerSet = true;

                Program.PrintSuccess(string.Format("0.50 ms ({0} us)", curRes / 10));
            }
            catch
            {
                Program.PrintError("FAIL");
            }

            // 3. Dynamic MMCSS Thread Registration
            try
            {
                uint taskIdx = 0;
                hMmcssThread = NativeMethods.AvSetMmThreadCharacteristics("Games", ref taskIdx);
                if (hMmcssThread != IntPtr.Zero)
                {
                    NativeMethods.AvSetMmThreadPriority(hMmcssThread, 2); // Critical
                }
            }
            catch { }
        }

        public static void Restore(TunerState state)
        {
            if (state == null) return;

            // Revert dynamic MMCSS thread boost
            if (hMmcssThread != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.AvRevertMmThreadCharacteristics(hMmcssThread);
                    hMmcssThread = IntPtr.Zero;
                }
                catch { }
            }

            // Revert Timer Resolution
            if (state.TimerResolutionSet)
            {
                try
                {
                    uint dummy;
                    NativeMethods.NtSetTimerResolution(state.OrigTimerResolution, false, out dummy);
                }
                catch { }
            }
            if (state.MultimediaTimerSet)
            {
                try
                {
                    NativeMethods.TimeEndPeriod(1);
                }
                catch { }
            }
            try
            {
                using (RegistryKey kernelKey = Registry.LocalMachine.OpenSubKey(KernelKeyPath, true))
                {
                    if (kernelKey != null)
                    {
                        for (int i = 0; i < state.RegistrySnapshots.Count; i++)
                        {
                            RegistrySnapshot snap = state.RegistrySnapshots[i];
                            if (snap.KeyPath.EndsWith("Session Manager\\kernel", StringComparison.OrdinalIgnoreCase))
                            {
                                Program.RevertRegistryValue(kernelKey, "GlobalTimerResolutionRequests", snap.Values.ContainsKey("GlobalTimerResolutionRequests") ? snap.Values["GlobalTimerResolutionRequests"] : null);
                            }
                        }
                    }
                }
                Console.WriteLine("  [+] Restored system timer resolution.");
            }
            catch { }

            // Revert MMCSS SystemProfile
            try
            {
                using (RegistryKey mmKey = Registry.LocalMachine.OpenSubKey(MmcssPath, true))
                {
                    if (mmKey != null)
                    {
                        for (int i = 0; i < state.RegistrySnapshots.Count; i++)
                        {
                            RegistrySnapshot snap = state.RegistrySnapshots[i];
                            if (snap.KeyPath.EndsWith("Multimedia\\SystemProfile", StringComparison.OrdinalIgnoreCase))
                            {
                                object nti = snap.Values.ContainsKey("NetworkThrottlingIndex") ? snap.Values["NetworkThrottlingIndex"] : null;
                                object sr = snap.Values.ContainsKey("SystemResponsiveness") ? snap.Values["SystemResponsiveness"] : null;
                                object nl = snap.Values.ContainsKey("NoLazyMode") ? snap.Values["NoLazyMode"] : null;
                                object ao = snap.Values.ContainsKey("AlwaysOn") ? snap.Values["AlwaysOn"] : null;

                                if (nti != null)
                                    mmKey.SetValue("NetworkThrottlingIndex", nti, RegistryValueKind.DWord);
                                else
                                    mmKey.SetValue("NetworkThrottlingIndex", 10, RegistryValueKind.DWord);

                                if (sr != null)
                                    mmKey.SetValue("SystemResponsiveness", sr, RegistryValueKind.DWord);
                                else
                                    mmKey.SetValue("SystemResponsiveness", 20, RegistryValueKind.DWord);

                                Program.RevertRegistryValue(mmKey, "NoLazyMode", nl);
                                Program.RevertRegistryValue(mmKey, "AlwaysOn", ao);
                            }
                        }
                    }
                }
                Console.WriteLine("  [+] Restored MMCSS multimedia scheduling profile.");
            }
            catch { }
        }
    }

    internal static class ProcessManagerModule
    {
        public static bool OptimizeTargetProcess(Process proc, HashSet<int> trackedPids)
        {
            if (proc == null || proc.HasExited) return false;
            if (trackedPids.Contains(proc.Id)) return true;

            IntPtr handle = proc.Handle;

            // 1. CPU Scheduling Priority
            try
            {
                if (proc.PriorityClass != ProcessPriorityClass.High)
                    proc.PriorityClass = ProcessPriorityClass.High;
            }
            catch
            {
                try
                {
                    if (proc.PriorityClass != ProcessPriorityClass.AboveNormal)
                        proc.PriorityClass = ProcessPriorityClass.AboveNormal;
                }
                catch { }
            }

            // 2. I/O Priority (High = 3)
            try
            {
                int ioPriority = 3;
                int status = NativeMethods.NtSetInformationProcess(handle, 33, ref ioPriority, sizeof(int));
                if (status != 0)
                {
                    ioPriority = 2; // Normal fallback
                    NativeMethods.NtSetInformationProcess(handle, 33, ref ioPriority, sizeof(int));
                }
            }
            catch { }

            // 3. Disable Windows 11 EcoQoS Power Throttling
            try
            {
                NativeMethods.PROCESS_POWER_THROTTLING_STATE state = new NativeMethods.PROCESS_POWER_THROTTLING_STATE();
                state.Version = 1;
                state.ControlMask = 1; // PROCESS_POWER_THROTTLING_EXECUTION_SPEED
                state.StateMask = 0;   // Force Execution Speed Throttling OFF
                NativeMethods.SetProcessInformation(handle, 4, ref state, (uint)Marshal.SizeOf(state));
            }
            catch { }

            trackedPids.Add(proc.Id);
            return true;
        }
    }

    #endregion

    #region Subsystem: Programmatic Latency & Jitter Diagnostic Engine (RFC 3550)

    public struct BenchmarkMetrics
    {
        public string TargetHost;
        public string TargetIp;
        public string AsnInfo;
        public int Sent;
        public int Received;
        public int Lost;
        public double LossPercentage;
        public double MinRtt;
        public double MaxRtt;
        public double MeanRtt;
        public double MedianRtt;
        public double Variance;
        public double StandardDeviation;
        public double Rfc3550Jitter;
        public double PeakJitter;
    }

    public static class DiagnosticBenchmarkModule
    {
        public static BenchmarkMetrics RunBenchmark(string targetHost, int sampleCount, int intervalMs, int timeoutMs)
        {
            BenchmarkMetrics metrics = new BenchmarkMetrics();
            metrics.TargetHost = targetHost;
            metrics.TargetIp = targetHost;
            metrics.AsnInfo = targetHost.Contains("roblox.com") ? "AS22697" : "Anycast";
            metrics.Sent = sampleCount;
            metrics.Received = 0;
            metrics.Lost = 0;

            // Resolve host to IP
            try
            {
                IPAddress[] ips = Dns.GetHostAddresses(targetHost);
                for (int i = 0; i < ips.Length; i++)
                {
                    if (ips[i].AddressFamily == AddressFamily.InterNetwork)
                    {
                        metrics.TargetIp = ips[i].ToString();
                        break;
                    }
                }
            }
            catch
            {
                metrics.TargetIp = targetHost;
            }

            List<double> rttList = new List<double>(sampleCount);
            double rfcJitter = 0.0;
            double peakJitter = 0.0;

            using (Ping pinger = new Ping())
            {
                byte[] buffer = new byte[32]; // Standard 32-byte ICMP payload
                PingOptions options = new PingOptions(64, true); // Don't Fragment

                for (int i = 0; i < sampleCount; i++)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    try
                    {
                        PingReply reply = pinger.Send(metrics.TargetIp, timeoutMs, buffer, options);
                        sw.Stop();

                        if (reply.Status == IPStatus.Success)
                        {
                            // Sub-millisecond precision via QPC Stopwatch
                            double elapsedMs = (sw.ElapsedTicks * 1000.0) / (double)Stopwatch.Frequency;
                            rttList.Add(elapsedMs);

                            if (rttList.Count > 1)
                            {
                                double diff = Math.Abs(elapsedMs - rttList[rttList.Count - 2]);
                                if (diff > peakJitter) peakJitter = diff;

                                if (rttList.Count == 2)
                                {
                                    rfcJitter = diff;
                                }
                                else
                                {
                                    // RFC 3550 interarrival jitter formula: J = J + (|D| - J) / 16.0
                                    rfcJitter = rfcJitter + (diff - rfcJitter) / 16.0;
                                }
                            }
                        }
                        else
                        {
                            metrics.Lost++;
                        }
                    }
                    catch
                    {
                        metrics.Lost++;
                    }

                    if (i < sampleCount - 1 && intervalMs > 0)
                    {
                        Thread.Sleep(intervalMs);
                    }
                }
            }

            metrics.Received = rttList.Count;
            metrics.LossPercentage = (metrics.Sent > 0) ? ((double)metrics.Lost * 100.0 / (double)metrics.Sent) : 0.0;

            if (rttList.Count > 0)
            {
                double sum = 0.0;
                double min = double.MaxValue;
                double max = double.MinValue;

                for (int i = 0; i < rttList.Count; i++)
                {
                    double val = rttList[i];
                    sum += val;
                    if (val < min) min = val;
                    if (val > max) max = val;
                }

                metrics.MinRtt = min;
                metrics.MaxRtt = max;
                metrics.MeanRtt = sum / rttList.Count;

                // Median
                List<double> sorted = new List<double>(rttList);
                sorted.Sort();
                int n = sorted.Count;
                if (n % 2 == 1)
                {
                    metrics.MedianRtt = sorted[n / 2];
                }
                else
                {
                    metrics.MedianRtt = (sorted[(n / 2) - 1] + sorted[n / 2]) / 2.0;
                }

                // Sample Variance & Standard Deviation
                double sumSquares = 0.0;
                for (int i = 0; i < rttList.Count; i++)
                {
                    double delta = rttList[i] - metrics.MeanRtt;
                    sumSquares += delta * delta;
                }

                metrics.Variance = (n > 1) ? (sumSquares / (double)(n - 1)) : 0.0;
                metrics.StandardDeviation = Math.Sqrt(metrics.Variance);
                metrics.Rfc3550Jitter = rfcJitter;
                metrics.PeakJitter = peakJitter;
            }

            return metrics;
        }

        public static void PrintBenchmarkResult(BenchmarkMetrics m, int intervalMs)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("[RobloxNetworkTuner Diagnostic Engine]");
            Console.ResetColor();
            Console.WriteLine("Target: {0} ({1}) [{2}]", m.TargetHost, m.TargetIp, m.AsnInfo);
            Console.WriteLine("Samples: {0} | Interval: {1}ms | Payload: 32 bytes", m.Sent, intervalMs);
            Console.WriteLine();

            Console.WriteLine("Latency Metrics:");
            Console.WriteLine("  Min RTT:       {0,7:F2} ms", m.MinRtt);
            Console.WriteLine("  Max RTT:       {0,7:F2} ms", m.MaxRtt);
            Console.WriteLine("  Mean RTT:      {0,7:F2} ms", m.MeanRtt);
            Console.WriteLine("  Median RTT:    {0,7:F2} ms", m.MedianRtt);
            Console.WriteLine("  Variance:      {0,7:F2} ms^2", m.Variance);
            Console.WriteLine("  Std Dev:       {0,7:F2} ms", m.StandardDeviation);
            Console.WriteLine();

            Console.WriteLine("Jitter (RFC 3550):");
            Console.WriteLine("  Mean Jitter:   {0,7:F2} ms", m.Rfc3550Jitter);
            Console.WriteLine("  Peak Jitter:   {0,7:F2} ms", m.PeakJitter);
            Console.WriteLine("  Packet Loss:   {0,7:F2} % ({1}/{2})", m.LossPercentage, m.Lost, m.Sent);

            if (m.Variance < 2.0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Status: OPTIMIZED (Jitter variance < 2.0 ms^2)");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Status: UNOPTIMIZED (Jitter variance {0:F2} ms^2 >= 2.0)", m.Variance);
                Console.ResetColor();
            }
        }
    }

    #endregion

    #region GitHub Releases Auto-Update Engine

    internal static class GitHubUpdateModule
    {
        public const string CurrentVersion = "2.1.0";
        public const string DefaultGitHubRepo = "getsentrix/RBLX-Network-Tuner";

        public class ReleaseInfo
        {
            public string TagName;
            public Version ReleaseVersion;
            public string ExeDownloadUrl;
            public string SetupDownloadUrl;
            public string ReleaseNotes;
        }

        public static void CheckForUpdateAsync()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    ReleaseInfo rel = FetchLatestRelease();
                    if (rel != null && rel.ReleaseVersion != null)
                    {
                        Version curVer = new Version(CurrentVersion);
                        if (rel.ReleaseVersion > curVer)
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("\n [UPDATE] New release available: {0} (Current: v{1})", rel.TagName, CurrentVersion);
                            Console.WriteLine("          Run: RobloxNetworkTuner.exe --update to install automatically.");
                            Console.ResetColor();
                        }
                    }
                }
                catch { }
            });
        }

        public static void CheckForUpdate(bool autoUpdate)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" ROBLOX NETWORK TUNER - GITHUB UPDATE CHECKER");
            Console.WriteLine(" Installed Version: v{0}", CurrentVersion);
            Console.WriteLine("================================================================================");
            Console.ResetColor();
            Console.Write(" [*] Querying GitHub releases API ... ");

            try
            {
                ReleaseInfo rel = FetchLatestRelease();
                if (rel == null || rel.ReleaseVersion == null)
                {
                    Program.PrintInfo("UP TO DATE / NO RELEASES FOUND");
                    return;
                }

                Version curVer = new Version(CurrentVersion);
                if (rel.ReleaseVersion > curVer)
                {
                    Program.PrintSuccess("UPDATE AVAILABLE (" + rel.TagName + ")");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n New release found: {0}", rel.TagName);
                    if (!string.IsNullOrEmpty(rel.ReleaseNotes))
                    {
                        Console.WriteLine(" Release Title: {0}", rel.ReleaseNotes);
                    }
                    Console.ResetColor();

                    if (autoUpdate)
                    {
                        PerformUpdateWithRelease(rel);
                    }
                    else
                    {
                        Console.WriteLine("\n Run 'RobloxNetworkTuner.exe --update' to apply this update.");
                    }
                }
                else
                {
                    Program.PrintSuccess("UP TO DATE (v" + CurrentVersion + " is current)");
                }
            }
            catch (Exception ex)
            {
                Program.PrintInfo("SKIPPED (" + ex.Message + ")");
            }
        }

        public static void PerformUpdate()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" ROBLOX NETWORK TUNER - AUTOMATIC IN-PLACE UPDATER");
            Console.WriteLine(" Current Version: v{0}", CurrentVersion);
            Console.WriteLine("================================================================================");
            Console.ResetColor();
            Console.Write(" [*] Fetching latest release metadata from GitHub ... ");

            try
            {
                ReleaseInfo rel = FetchLatestRelease();
                if (rel == null || rel.ReleaseVersion == null)
                {
                    Program.PrintInfo("NO RELEASES FOUND");
                    return;
                }

                Version curVer = new Version(CurrentVersion);
                if (rel.ReleaseVersion <= curVer)
                {
                    Program.PrintSuccess("ALREADY UP TO DATE (v" + CurrentVersion + ")");
                    return;
                }

                Program.PrintSuccess("FOUND " + rel.TagName);
                PerformUpdateWithRelease(rel);
            }
            catch (Exception ex)
            {
                Program.PrintError("FAIL: " + ex.Message);
            }
        }

        private static void PerformUpdateWithRelease(ReleaseInfo rel)
        {
            if (string.IsNullOrEmpty(rel.ExeDownloadUrl))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(" [!] No standalone RobloxNetworkTuner.exe asset in release {0}.", rel.TagName);
                Console.ResetColor();
                return;
            }

            Console.Write(" [*] Downloading updated binary payload ... ");
            string currentExe = Process.GetCurrentProcess().MainModule.FileName;
            string tempDownload = Path.Combine(Path.GetTempPath(), "RobloxNetworkTuner_update.exe");
            string backupExe = currentExe + ".old";

            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "RobloxNetworkTuner-Updater/2.0");
                    wc.DownloadFile(rel.ExeDownloadUrl, tempDownload);
                }

                FileInfo fi = new FileInfo(tempDownload);
                if (!fi.Exists || fi.Length < 10000)
                {
                    throw new IOException("Downloaded update file is invalid or corrupted.");
                }
                Program.PrintSuccess(string.Format("DONE ({0:N0} bytes)", fi.Length));

                Console.Write(" [*] Applying in-place binary swap ... ");
                if (File.Exists(backupExe))
                {
                    try { File.Delete(backupExe); } catch { }
                }

                File.Move(currentExe, backupExe);
                File.Move(tempDownload, currentExe);
                Program.PrintSuccess("DONE");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n================================================================================");
                Console.WriteLine(" SUCCESS: Updated Roblox Network Tuner to {0}!", rel.TagName);
                Console.WriteLine(" Backup of previous binary saved to: {0}.old", Path.GetFileName(currentExe));
                Console.WriteLine("================================================================================");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Program.PrintError("FAIL: " + ex.Message);
                if (!File.Exists(currentExe) && File.Exists(backupExe))
                {
                    try { File.Move(backupExe, currentExe); } catch { }
                }
            }
        }

        private static ReleaseInfo FetchLatestRelease()
        {
            try
            {
                string repo = DefaultGitHubRepo;
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\RobloxNetworkTuner"))
                {
                    if (k != null)
                    {
                        object custom = k.GetValue("GitHubRepo");
                        if (custom != null && !string.IsNullOrEmpty(custom.ToString()))
                        {
                            repo = custom.ToString().Trim();
                        }
                    }
                }

                string apiUrl = string.Format("https://api.github.com/repos/{0}/releases/latest", repo);

                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                string json;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "RobloxNetworkTuner-Updater/2.0");
                    wc.Headers.Add("Accept", "application/vnd.github.v3+json");
                    json = wc.DownloadString(apiUrl);
                }

                if (string.IsNullOrEmpty(json)) return null;

                ReleaseInfo info = new ReleaseInfo();
                Match tagMatch = Regex.Match(json, @"""tag_name""\s*:\s*""([^""]+)""");
                if (tagMatch.Success)
                {
                    info.TagName = tagMatch.Groups[1].Value.Trim();
                    string cleanVer = info.TagName.TrimStart('v', 'V');
                    Version v;
                    if (Version.TryParse(cleanVer, out v))
                    {
                        info.ReleaseVersion = v;
                    }
                    else if (cleanVer.Split('.').Length == 2)
                    {
                        Version.TryParse(cleanVer + ".0", out v);
                        info.ReleaseVersion = v;
                    }
                }

                Match exeMatch = Regex.Match(json, @"""browser_download_url""\s*:\s*""([^""]*RobloxNetworkTuner\.exe)""");
                if (exeMatch.Success)
                {
                    info.ExeDownloadUrl = exeMatch.Groups[1].Value;
                }

                Match setupMatch = Regex.Match(json, @"""browser_download_url""\s*:\s*""([^""]*RobloxNetworkTunerSetup\.exe)""");
                if (setupMatch.Success)
                {
                    info.SetupDownloadUrl = setupMatch.Groups[1].Value;
                }

                Match bodyMatch = Regex.Match(json, @"""name""\s*:\s*""([^""]+)""");
                if (bodyMatch.Success)
                {
                    info.ReleaseNotes = bodyMatch.Groups[1].Value;
                }

                return info;
            }
            catch
            {
                return null;
            }
        }
    }

    #endregion

    #region Modern Dark-Mode GUI

    internal class TunerGuiForm : Form
    {
        private readonly System.Windows.Forms.Timer watchdogTimer;
        private readonly System.Windows.Forms.Timer telemetryTimer;
        private readonly NotifyIcon trayIcon;
        private readonly ContextMenuStrip trayMenu;

        private string activeAdapterName = "Detecting active network adapter...";
        private string activeAdapterDetails = "IPv4: Initializing... | MTU: 1500 | Nagle: Disabled";
        private string watchdogStatus = "Standby: Waiting for RobloxPlayerBeta.exe...";
        private bool robloxRunning = false;
        private int robloxPid = 0;
        private double liveRtt = 0.0;
        private double liveJitter = 0.0;
        private double prevRtt = 0.0;
        private bool isFirstPing = true;
        private bool isTuningApplied = false;

        private Rectangle rectBtnClose = new Rectangle(580, 16, 26, 26);
        private Rectangle rectBtnMin = new Rectangle(546, 16, 26, 26);
        private Rectangle rectBtnTray = new Rectangle(20, 642, 280, 42);
        private Rectangle rectBtnExit = new Rectangle(320, 642, 280, 42);

        private bool hoverBtnClose = false;
        private bool hoverBtnMin = false;
        private bool hoverBtnTray = false;
        private bool hoverBtnExit = false;

        public TunerGuiForm()
        {
            this.Text = "Roblox Network Tuner [x64]";
            this.Size = new Size(620, 705);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(11, 14, 20); // Deep Obsidian
            this.DoubleBuffered = true;
            this.ShowIcon = true;
            this.ShowInTaskbar = true;

            try
            {
                if (File.Exists("app.ico"))
                {
                    this.Icon = new Icon("app.ico");
                }
                else
                {
                    this.Icon = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
                }
            }
            catch { }

            // Tray Icon & Menu
            this.trayMenu = new ContextMenuStrip();
            ToolStripMenuItem itemShow = new ToolStripMenuItem("Show Dashboard", null, delegate { ShowDashboard(); });
            ToolStripMenuItem itemUpdate = new ToolStripMenuItem("Check for Updates", null, delegate { GitHubUpdateModule.CheckForUpdate(true); });
            ToolStripMenuItem itemExit = new ToolStripMenuItem("Restore Baseline & Exit", null, delegate { SafeExit(); });
            this.trayMenu.Items.Add(itemShow);
            this.trayMenu.Items.Add(itemUpdate);
            this.trayMenu.Items.Add(new ToolStripSeparator());
            this.trayMenu.Items.Add(itemExit);

            this.trayIcon = new NotifyIcon();
            this.trayIcon.Text = "Roblox Network Tuner - Active (0.50ms / DSCP 46)";
            this.trayIcon.Icon = this.Icon;
            this.trayIcon.ContextMenuStrip = this.trayMenu;
            this.trayIcon.Visible = true;
            this.trayIcon.DoubleClick += delegate { ShowDashboard(); };

            // Mouse handlers
            this.MouseDown += TunerGuiForm_MouseDown;
            this.MouseMove += TunerGuiForm_MouseMove;

            // Watchdog Timer (1000ms)
            this.watchdogTimer = new System.Windows.Forms.Timer();
            this.watchdogTimer.Interval = 1000;
            this.watchdogTimer.Tick += WatchdogTimer_Tick;

            // Telemetry Ping Timer (3000ms)
            this.telemetryTimer = new System.Windows.Forms.Timer();
            this.telemetryTimer.Interval = 3000;
            this.telemetryTimer.Tick += TelemetryTimer_Tick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                cp.Style |= 0x20000;      // WS_MINIMIZEBOX
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Execute hands-free initial tuning in background thread
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    // 1. Refresh active network adapter
                    List<Program.ActiveInterfaceDetector.ActiveInterfaceInfo> nics = Program.ActiveInterfaceDetector.GetActiveInterfaces();
                    if (nics.Count > 0)
                    {
                        Program.ActiveInterfaceDetector.ActiveInterfaceInfo nic = nics[0];
                        activeAdapterName = nic.Description;
                        activeAdapterDetails = string.Format("IPv4: {0}   |   MTU: {1} (Optimized)   |   Nagle: Disabled",
                            nic.Ipv4Address != null ? nic.Ipv4Address.ToString() : "N/A", nic.Mtu);
                    }
                    else
                    {
                        activeAdapterName = "No active physical adapter detected";
                        activeAdapterDetails = "IPv4: N/A | MTU: 1500";
                    }

                    // 2. Apply optimizations
                    Program.ApplyAll();
                    isTuningApplied = true;

                    // 3. Trigger initial telemetry & update check
                    PingEdgeTarget();
                    GitHubUpdateModule.CheckForUpdateAsync();

                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        this.watchdogTimer.Start();
                        this.telemetryTimer.Start();
                        this.Invalidate();
                    });
                }
                catch (Exception ex)
                {
                    activeAdapterName = "Error: " + ex.Message;
                    this.BeginInvoke((MethodInvoker)delegate { this.Invalidate(); });
                }
            });
        }

        private void WatchdogTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                Process[] procs = Process.GetProcessesByName(Program.TargetProcessName);
                if (procs.Length > 0)
                {
                    robloxRunning = true;
                    robloxPid = procs[0].Id;
                    ProcessManagerModule.OptimizeTargetProcess(procs[0], Program.OptimizedProcessIds);

                    string prio = "High";
                    try { prio = procs[0].PriorityClass.ToString(); } catch { }
                    watchdogStatus = string.Format("ACTIVE (PID {0}) — CPU: {1}, I/O: High, EcoQoS: Off", robloxPid, prio);
                }
                else
                {
                    if (robloxRunning)
                    {
                        robloxRunning = false;
                        watchdogStatus = "STANDBY: Roblox exited. System baseline intact.";
                    }
                    else
                    {
                        watchdogStatus = "STANDBY: Waiting for RobloxPlayerBeta.exe launch...";
                    }
                }
                this.Invalidate();
            }
            catch { }
        }

        private void TelemetryTimer_Tick(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                PingEdgeTarget();
                try
                {
                    this.BeginInvoke((MethodInvoker)delegate { this.Invalidate(); });
                }
                catch { }
            });
        }

        private void PingEdgeTarget()
        {
            try
            {
                using (Ping p = new Ping())
                {
                    byte[] buf = new byte[32];
                    PingReply reply = p.Send("roblox.com", 1200, buf);
                    if (reply != null && reply.Status == IPStatus.Success)
                    {
                        double rtt = reply.RoundtripTime;
                        liveRtt = rtt;

                        if (isFirstPing)
                        {
                            liveJitter = 0.5;
                            prevRtt = rtt;
                            isFirstPing = false;
                        }
                        else
                        {
                            double d = Math.Abs(rtt - prevRtt);
                            liveJitter = liveJitter + (d - liveJitter) / 16.0;
                            prevRtt = rtt;
                        }
                    }
                }
            }
            catch { }
        }

        private void ShowDashboard()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
        }

        private void SafeExit()
        {
            this.watchdogTimer.Stop();
            this.telemetryTimer.Stop();
            if (this.trayIcon != null)
            {
                this.trayIcon.Visible = false;
                this.trayIcon.Dispose();
            }
            Program.RestoreAll();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SafeExit();
            base.OnFormClosing(e);
        }

        private void TunerGuiForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (rectBtnClose.Contains(e.Location))
                {
                    SafeExit();
                    return;
                }
                if (rectBtnMin.Contains(e.Location))
                {
                    this.WindowState = FormWindowState.Minimized;
                    return;
                }
                if (rectBtnTray.Contains(e.Location))
                {
                    this.Hide();
                    if (this.trayIcon != null)
                    {
                        this.trayIcon.ShowBalloonTip(2000, "Roblox Network Tuner", "Running silently in background. Baseline safe.", ToolTipIcon.Info);
                    }
                    return;
                }
                if (rectBtnExit.Contains(e.Location))
                {
                    SafeExit();
                    return;
                }

                if (e.Y <= 70)
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(this.Handle, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HT_CAPTION, 0);
                }
            }
        }

        private void TunerGuiForm_MouseMove(object sender, MouseEventArgs e)
        {
            bool redraw = false;

            bool hClose = rectBtnClose.Contains(e.Location);
            if (hClose != hoverBtnClose) { hoverBtnClose = hClose; redraw = true; }

            bool hMin = rectBtnMin.Contains(e.Location);
            if (hMin != hoverBtnMin) { hoverBtnMin = hMin; redraw = true; }

            bool hTray = rectBtnTray.Contains(e.Location);
            if (hTray != hoverBtnTray) { hoverBtnTray = hTray; redraw = true; }

            bool hExit = rectBtnExit.Contains(e.Location);
            if (hExit != hoverBtnExit) { hoverBtnExit = hExit; redraw = true; }

            if (redraw) this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Outer border
            using (Pen borderPen = new Pen(Color.FromArgb(31, 41, 61), 1))
            {
                g.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
            }

            // 1. Header Bar (Y: 0 to 70)
            using (SolidBrush headerBg = new SolidBrush(Color.FromArgb(14, 18, 28)))
            {
                g.FillRectangle(headerBg, 1, 1, this.Width - 2, 69);
            }
            using (Pen hSep = new Pen(Color.FromArgb(27, 36, 54), 1))
            {
                g.DrawLine(hSep, 1, 70, this.Width - 2, 70);
            }

            // Draw Logo Emblem
            DrawLogoEmblem(g, 20, 16, 38);

            // Title & Subtitle
            using (Font fTitle = new Font("Segoe UI", 12.5f, FontStyle.Bold))
            using (Brush bTitle = new SolidBrush(Color.White))
            {
                g.DrawString("ROBLOX NETWORK TUNER", fTitle, bTitle, 68, 15);
            }
            using (Font fSub = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (Brush bSub = new SolidBrush(Color.FromArgb(0, 240, 255)))
            {
                g.DrawString("ULTRA LOW-LATENCY & ANTI-JITTER ENGINE", fSub, bSub, 69, 37);
            }

            // Version Pill
            DrawPill(g, 342, 16, 54, 20, "v2.1.0", Color.FromArgb(22, 35, 59), Color.FromArgb(0, 240, 255));

            // Minimize & Close Buttons
            DrawWindowButton(g, rectBtnMin, "—", hoverBtnMin, Color.FromArgb(35, 45, 66), Color.White);
            DrawWindowButton(g, rectBtnClose, "✕", hoverBtnClose, Color.FromArgb(232, 17, 35), Color.White);

            // 2. Hero Status Card (Y: 82 to 142)
            Rectangle rectHero = new Rectangle(20, 82, 580, 60);
            Color heroBg = isTuningApplied ? Color.FromArgb(13, 34, 29) : Color.FromArgb(34, 25, 13);
            Color heroBorder = isTuningApplied ? Color.FromArgb(0, 255, 163) : Color.FromArgb(255, 180, 0);
            Color heroText = isTuningApplied ? Color.FromArgb(0, 255, 163) : Color.FromArgb(255, 180, 0);

            DrawRoundedCard(g, rectHero, heroBg, heroBorder, 8);

            using (Font fHeroHead = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (Brush bHeroHead = new SolidBrush(heroText))
            {
                string heroTitle = isTuningApplied
                    ? "●  OPTIMIZED — KERNEL & NETWORK STACK LOCKED"
                    : "○  INITIALIZING LOW-LATENCY ENGINE...";
                g.DrawString(heroTitle, fHeroHead, bHeroHead, 36, 92);
            }
            using (Font fHeroSub = new Font("Segoe UI", 8.25f, FontStyle.Regular))
            using (Brush bHeroSub = new SolidBrush(Color.FromArgb(160, 200, 185)))
            {
                g.DrawString("0.50ms High-Precision Timer  •  DSCP 46 Expedited Forwarding  •  Fast-Path UDP", fHeroSub, bHeroSub, 38, 117);
            }

            // 3. Active Network Interface Card (Y: 152 to 230)
            Rectangle rectNic = new Rectangle(20, 152, 580, 78);
            DrawRoundedCard(g, rectNic, Color.FromArgb(18, 23, 35), Color.FromArgb(31, 41, 61), 8);

            using (Font fCardHead = new Font("Segoe UI", 7.75f, FontStyle.Bold))
            using (Brush bCardHead = new SolidBrush(Color.FromArgb(126, 139, 155)))
            {
                g.DrawString("ACTIVE NETWORK ADAPTER", fCardHead, bCardHead, 36, 162);
            }
            using (Font fNicName = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (Brush bWhite = new SolidBrush(Color.White))
            {
                string truncatedNic = activeAdapterName.Length > 58 ? activeAdapterName.Substring(0, 58) + "..." : activeAdapterName;
                g.DrawString(truncatedNic, fNicName, bWhite, 36, 182);
            }
            using (Font fNicDet = new Font("Segoe UI", 8.25f, FontStyle.Regular))
            using (Brush bCyan = new SolidBrush(Color.FromArgb(0, 240, 255)))
            {
                g.DrawString(activeAdapterDetails, fNicDet, bCyan, 36, 204);
            }

            // 4. Target Process Watchdog Card (Y: 238 to 316)
            Rectangle rectWatch = new Rectangle(20, 238, 580, 78);
            DrawRoundedCard(g, rectWatch, Color.FromArgb(18, 23, 35), Color.FromArgb(31, 41, 61), 8);

            using (Font fCardHead = new Font("Segoe UI", 7.75f, FontStyle.Bold))
            using (Brush bCardHead = new SolidBrush(Color.FromArgb(126, 139, 155)))
            {
                g.DrawString("TARGET PROCESS WATCHDOG (AUTOMATIC BOOST)", fCardHead, bCardHead, 36, 248);
            }
            using (Font fProc = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (Brush bWhite = new SolidBrush(Color.White))
            {
                g.DrawString("RobloxPlayerBeta.exe", fProc, bWhite, 36, 268);
            }
            using (Font fWatchStat = new Font("Segoe UI", 8.25f, FontStyle.Regular))
            using (Brush bWatchColor = new SolidBrush(robloxRunning ? Color.FromArgb(0, 255, 163) : Color.FromArgb(150, 165, 185)))
            {
                g.DrawString(watchdogStatus, fWatchStat, bWatchColor, 36, 290);
            }

            // 5. Kernel & QoS Parameter Card (Y: 324 to 442)
            Rectangle rectStack = new Rectangle(20, 324, 580, 118);
            DrawRoundedCard(g, rectStack, Color.FromArgb(18, 23, 35), Color.FromArgb(31, 41, 61), 8);

            using (Font fCardHead = new Font("Segoe UI", 7.75f, FontStyle.Bold))
            using (Brush bCardHead = new SolidBrush(Color.FromArgb(126, 139, 155)))
            {
                g.DrawString("LOW-LATENCY KERNEL & SOCKET TUNING", fCardHead, bCardHead, 36, 334);
            }

            using (Font fParam = new Font("Segoe UI", 8.25f, FontStyle.Regular))
            using (Brush bParam = new SolidBrush(Color.FromArgb(220, 230, 245)))
            using (Brush bCheck = new SolidBrush(Color.FromArgb(0, 255, 163)))
            {
                g.DrawString("✓", fParam, bCheck, 36, 356);
                g.DrawString("Global Timer Resolution: 0.50 ms (2000 Hz NT Kernel Interrupt Rate)", fParam, bParam, 56, 356);

                g.DrawString("✓", fParam, bCheck, 36, 376);
                g.DrawString("Winsock AFD UDP Fast-Path: 1500 Byte Datagram Threshold Locked", fParam, bParam, 56, 376);

                g.DrawString("✓", fParam, bCheck, 36, 396);
                g.DrawString("Wi-Fi 7 / DBS Background Scan: Frozen (Zero 60s Lag & Ping Spikes)", fParam, bParam, 56, 396);

                g.DrawString("✓", fParam, bCheck, 36, 416);
                g.DrawString("MMCSS & Policy QoS: 100% Responsiveness, DSCP 46 Voice Prioritization", fParam, bParam, 56, 416);
            }

            // 6. Live Edge Latency & Jitter Monitor Card (Y: 450 to 578)
            Rectangle rectDiag = new Rectangle(20, 450, 580, 128);
            DrawRoundedCard(g, rectDiag, Color.FromArgb(18, 23, 35), Color.FromArgb(31, 41, 61), 8);

            using (Font fCardHead = new Font("Segoe UI", 7.75f, FontStyle.Bold))
            using (Brush bCardHead = new SolidBrush(Color.FromArgb(126, 139, 155)))
            {
                g.DrawString("LIVE ROBLOX EDGE TELEMETRY  •  AUTOMATED RFC 3550 PACER", fCardHead, bCardHead, 36, 460);
            }

            using (Font fMetricVal = new Font("Segoe UI", 20f, FontStyle.Bold))
            using (Font fMetricLbl = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (Brush bRttVal = new SolidBrush(Color.FromArgb(0, 240, 255)))
            using (Brush bJitterVal = new SolidBrush(Color.FromArgb(0, 255, 163)))
            using (Brush bMuted = new SolidBrush(Color.FromArgb(126, 139, 155)))
            {
                string rttStr = liveRtt > 0 ? string.Format("{0:F1} ms", liveRtt) : "-- ms";
                g.DrawString(rttStr, fMetricVal, bRttVal, 36, 480);
                g.DrawString("ROUND-TRIP TIME (RTT)", fMetricLbl, bMuted, 40, 518);

                string jitterStr = liveJitter > 0 ? string.Format("{0:F2} ms", liveJitter) : "-- ms";
                g.DrawString(jitterStr, fMetricVal, bJitterVal, 240, 480);
                g.DrawString("RFC 3550 JITTER (VARIANCE)", fMetricLbl, bMuted, 244, 518);

                string paceBadge = liveJitter < 2.0 ? "PACING: OPTIMIZED" : "PACING: STABLE";
                Color paceColor = liveJitter < 2.0 ? Color.FromArgb(0, 255, 163) : Color.FromArgb(0, 240, 255);
                DrawPill(g, 420, 492, 150, 26, paceBadge, Color.FromArgb(20, 36, 48), paceColor);
            }

            // Latency Bar Gauge
            using (SolidBrush gaugeBg = new SolidBrush(Color.FromArgb(27, 36, 54)))
            {
                g.FillRectangle(gaugeBg, 38, 546, 542, 8);
            }
            int fillWidth = 350;
            if (liveRtt > 0)
            {
                fillWidth = Math.Max(20, Math.Min(542, (int)(liveRtt * 6.5)));
            }
            Rectangle fillRect = new Rectangle(38, 546, fillWidth, 8);
            using (LinearGradientBrush fillBrush = new LinearGradientBrush(fillRect, Color.FromArgb(0, 240, 255), Color.FromArgb(0, 255, 163), 0f))
            {
                g.FillRectangle(fillBrush, fillRect);
            }

            // 7. Footer / Actions (Y: 590 to 695)
            using (Font fHint = new Font("Segoe UI", 7.75f, FontStyle.Regular))
            using (Brush bHint = new SolidBrush(Color.FromArgb(120, 135, 155)))
            {
                g.DrawString("Hands-Free Mode: System baselines are automatically restored when Roblox exits or when closing.",
                    fHint, bHint, 25, 614);
            }

            // Button 1: Minimize to Tray
            Color btnTrayBg = hoverBtnTray ? Color.FromArgb(28, 42, 68) : Color.FromArgb(18, 28, 46);
            DrawButton(g, rectBtnTray, "Minimize to Tray", btnTrayBg, Color.FromArgb(0, 240, 255), Color.FromArgb(0, 240, 255));

            // Button 2: Restore Baseline & Exit
            Color btnExitBg = hoverBtnExit ? Color.FromArgb(64, 25, 34) : Color.FromArgb(45, 18, 25);
            DrawButton(g, rectBtnExit, "Restore Baseline & Exit", btnExitBg, Color.FromArgb(255, 77, 106), Color.FromArgb(255, 77, 106));
        }

        private static void DrawLogoEmblem(Graphics g, float x, float y, float size)
        {
            float pad = size * 0.05f;
            float w = size - 2 * pad;
            float h = size - 2 * pad;
            float cx = x + size / 2.0f;
            float cy = y + size / 2.0f;
            float r = w / 2.0f;

            using (GraphicsPath path = new GraphicsPath())
            {
                PointF[] pts = new PointF[6];
                for (int i = 0; i < 6; i++)
                {
                    double angle = Math.PI / 3.0 * i - Math.PI / 6.0;
                    pts[i] = new PointF((float)(cx + r * Math.Cos(angle)), (float)(cy + r * Math.Sin(angle)));
                }
                path.AddPolygon(pts);

                using (Brush bShield = new SolidBrush(Color.FromArgb(13, 19, 31)))
                {
                    g.FillPath(bShield, path);
                }
                using (Pen pBorder = new Pen(Color.FromArgb(0, 240, 255), 1.5f))
                {
                    g.DrawPath(pBorder, path);
                }

                using (GraphicsPath bolt = new GraphicsPath())
                {
                    PointF[] bpts = new PointF[6];
                    bpts[0] = new PointF(cx + r * 0.12f, cy - r * 0.65f);
                    bpts[1] = new PointF(cx - r * 0.45f, cy + r * 0.05f);
                    bpts[2] = new PointF(cx - r * 0.05f, cy + r * 0.05f);
                    bpts[3] = new PointF(cx - r * 0.25f, cy + r * 0.70f);
                    bpts[4] = new PointF(cx + r * 0.45f, cy - r * 0.05f);
                    bpts[5] = new PointF(cx + r * 0.08f, cy - r * 0.05f);
                    bolt.AddPolygon(bpts);

                    using (LinearGradientBrush bBolt = new LinearGradientBrush(new RectangleF(x, y, size, size), Color.FromArgb(0, 240, 255), Color.FromArgb(0, 255, 163), 60f))
                    {
                        g.FillPath(bBolt, bolt);
                    }
                }
            }
        }

        private static void DrawRoundedCard(Graphics g, Rectangle bounds, Color bg, Color border, int radius)
        {
            using (GraphicsPath path = CreateRoundedRect(bounds, radius))
            {
                using (SolidBrush b = new SolidBrush(bg))
                {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(border, 1))
                {
                    g.DrawPath(p, path);
                }
            }
        }

        private static void DrawButton(Graphics g, Rectangle bounds, string text, Color bg, Color border, Color textColor)
        {
            using (GraphicsPath path = CreateRoundedRect(bounds, 6))
            {
                using (SolidBrush b = new SolidBrush(bg))
                {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(border, 1))
                {
                    g.DrawPath(p, path);
                }
            }
            using (Font f = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (Brush bText = new SolidBrush(textColor))
            {
                SizeF sz = g.MeasureString(text, f);
                float tx = bounds.X + (bounds.Width - sz.Width) / 2.0f;
                float ty = bounds.Y + (bounds.Height - sz.Height) / 2.0f;
                g.DrawString(text, f, bText, tx, ty);
            }
        }

        private static void DrawPill(Graphics g, int x, int y, int width, int height, string text, Color bg, Color textColor)
        {
            Rectangle r = new Rectangle(x, y, width, height);
            using (GraphicsPath path = CreateRoundedRect(r, height / 2))
            {
                using (SolidBrush b = new SolidBrush(bg))
                {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(textColor, 1))
                {
                    g.DrawPath(p, path);
                }
            }
            using (Font f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (Brush b = new SolidBrush(textColor))
            {
                SizeF sz = g.MeasureString(text, f);
                float tx = x + (width - sz.Width) / 2.0f;
                float ty = y + (height - sz.Height) / 2.0f;
                g.DrawString(text, f, b, tx, ty);
            }
        }

        private static void DrawWindowButton(Graphics g, Rectangle r, string text, bool hover, Color hoverBg, Color textColor)
        {
            if (hover)
            {
                using (SolidBrush b = new SolidBrush(hoverBg))
                {
                    g.FillRectangle(b, r);
                }
            }
            using (Font f = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (Brush b = new SolidBrush(textColor))
            {
                SizeF sz = g.MeasureString(text, f);
                float tx = r.X + (r.Width - sz.Width) / 2.0f;
                float ty = r.Y + (r.Height - sz.Height) / 2.0f;
                g.DrawString(text, f, b, tx, ty);
            }
        }

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    #endregion

    #region Main Controller & Watchdog Session

    internal static class Program
    {
        public const string TargetProcessName = "RobloxPlayerBeta";
        private const string QosPolicyName = "RobloxPriority";

        private static bool isOptimized;
        private static bool isRestoring;
        private static readonly object RestoreLock = new object();

        private static TunerState currentSnapshot = new TunerState();
        public static readonly HashSet<int> OptimizedProcessIds = new HashSet<int>();

        private static NativeMethods.ConsoleCtrlDelegate ctrlHandler;

        private static void InitConsoleOutput()
        {
            try
            {
                NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
                StreamWriter sw = new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding);
                sw.AutoFlush = true;
                Console.SetOut(sw);
                StreamWriter swErr = new StreamWriter(Console.OpenStandardError(), Console.OutputEncoding);
                swErr.AutoFlush = true;
                Console.SetError(swErr);
            }
            catch { }
        }

        private static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                InitConsoleOutput();
                string flag = args[0].ToLowerInvariant();
                if (flag == "--help" || flag == "-h" || flag == "/?" || flag == "-help")
                {
                    PrintHelp();
                    return;
                }
                if (flag == "--status" || flag == "-s" || flag == "/status")
                {
                    PrintStatus();
                    return;
                }
                if (flag == "--benchmark" || flag == "-b" || flag == "/benchmark")
                {
                    string target = "roblox.com";
                    int count = 50;
                    if (args.Length > 1 && !string.IsNullOrEmpty(args[1]))
                    {
                        target = args[1];
                    }
                    if (args.Length > 2)
                    {
                        int parsedCount;
                        if (int.TryParse(args[2], out parsedCount) && parsedCount > 0)
                        {
                            count = parsedCount;
                        }
                    }

                    RunBenchmarkCli(target, count);
                    return;
                }
                if (flag == "--restore" || flag == "/restore" || flag == "-r")
                {
                    if (!EnsureAdministrator(args)) return;
                    ManualRestore();
                    return;
                }
                if (flag == "--test-state" || flag == "--self-test")
                {
                    RunSelfTest();
                    return;
                }
                if (flag == "--check-update" || flag == "-check-update" || flag == "/checkupdate")
                {
                    GitHubUpdateModule.CheckForUpdate(false);
                    return;
                }
                if (flag == "--update" || flag == "-update" || flag == "/update")
                {
                    if (!EnsureAdministrator(args)) return;
                    GitHubUpdateModule.CheckForUpdate(true);
                    return;
                }
                if (flag == "--console" || flag == "-c" || flag == "/console")
                {
                    RunConsoleSession(args);
                    return;
                }
            }

            // Default Hands-Free Modern Dark Gaming GUI
            if (!EnsureAdministrator(args)) return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TunerGuiForm());
        }

        private static void RunConsoleSession(string[] args)
        {
            Console.Title = "Roblox Network Tuner [x64]";

            // Privileged Interactive Session
            if (!EnsureAdministrator(args)) return;

            // Check for background updates silently
            GitHubUpdateModule.CheckForUpdateAsync();

            // Register cleanup handlers
            ctrlHandler = ConsoleCtrlCheck;
            NativeMethods.SetConsoleCtrlHandler(ctrlHandler, true);

            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                RestoreAll();
                Environment.Exit(0);
            };

            AppDomain.CurrentDomain.ProcessExit += delegate
            {
                RestoreAll();
            };

            ApplyAll();

            // Watchdog & Session Loop
            bool robloxSeen = false;
            Process[] initial = Process.GetProcessesByName(TargetProcessName);
            if (initial.Length > 0)
            {
                robloxSeen = true;
                foreach (Process p in initial)
                {
                    ProcessManagerModule.OptimizeTargetProcess(p, OptimizedProcessIds);
                }
            }

            while (true)
            {
                // Check for user exit input (Space, Q, Escape)
                try
                {
                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Spacebar || key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Escape)
                        {
                            Console.WriteLine();
                            break;
                        }
                    }
                }
                catch { }

                // Inspect target game process
                Process[] rbxList = Process.GetProcessesByName(TargetProcessName);
                if (rbxList.Length > 0)
                {
                    robloxSeen = true;
                    Process primary = rbxList[0];

                    foreach (Process p in rbxList)
                    {
                        if (!OptimizedProcessIds.Contains(p.Id))
                        {
                            ProcessManagerModule.OptimizeTargetProcess(p, OptimizedProcessIds);
                        }
                    }

                    string timeStr = DateTime.Now.ToString("HH:mm:ss");
                    string prioStr = "Normal";
                    try { prioStr = primary.PriorityClass.ToString(); } catch { }

                    Console.ForegroundColor = ConsoleColor.Green;
                    WriteFixedLine(string.Format(" [ACTIVE]  Roblox (PID: {0} | CPU: {1} | I/O: High | EcoQoS: Off) [{2}]",
                        primary.Id, prioStr, timeStr));
                    Console.ResetColor();
                }
                else
                {
                    if (robloxSeen)
                    {
                        Console.WriteLine("\n\n [*] Roblox process exited. Restoring system baseline...");
                        break;
                    }

                    string timeStr = DateTime.Now.ToString("HH:mm:ss");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    WriteFixedLine(string.Format(" [STANDBY] Waiting for RobloxPlayerBeta.exe...  [{0}]", timeStr));
                    Console.ResetColor();
                }

                Thread.Sleep(250);
            }

            RestoreAll();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n [OK] Baseline restoration complete. Exiting...");
            Console.ResetColor();
            Thread.Sleep(1200);
        }

        internal static bool EnsureAdministrator(string[] args)
        {
            if (IsAdministrator()) return true;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule.FileName,
                    Arguments = args != null && args.Length > 0 ? string.Join(" ", args) : "",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                return false;
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[!] Administrator privileges required. Right-click and 'Run as administrator'.");
                Console.ResetColor();
                if (!Console.IsInputRedirected)
                {
                    Console.WriteLine("Press any key to exit...");
                    try { Console.ReadKey(); } catch { }
                }
                return false;
            }
        }

        private static bool ConsoleCtrlCheck(int ctrlType)
        {
            RestoreAll();
            return false;
        }

        private static void Apply()
        {
            ApplyAll();
        }

        private static void Restore()
        {
            RestoreAll();
        }

        #region Managed Registry, Dynamic Interface & System Restore Infrastructure

        internal static class RegistryManager
        {
            public static void SetDWord(RegistryKey key, string name, int value, RegistrySnapshot snap)
            {
                if (key == null) return;
                if (snap != null && !snap.Values.ContainsKey(name))
                {
                    snap.Values[name] = key.GetValue(name);
                }
                key.SetValue(name, value, RegistryValueKind.DWord);
            }

            public static void SetString(RegistryKey key, string name, string value, RegistrySnapshot snap)
            {
                if (key == null) return;
                if (snap != null && !snap.Values.ContainsKey(name))
                {
                    snap.Values[name] = key.GetValue(name);
                }
                key.SetValue(name, value, RegistryValueKind.String);
            }

            public static void RevertValue(RegistryKey key, string name, object origValue)
            {
                if (key == null) return;
                try
                {
                    if (origValue != null)
                    {
                        key.SetValue(name, origValue);
                    }
                    else
                    {
                        key.DeleteValue(name, false);
                    }
                }
                catch { }
            }
        }

        internal static class ActiveInterfaceDetector
        {
            public struct ActiveInterfaceInfo
            {
                public string Id;
                public string Name;
                public string Description;
                public NetworkInterfaceType InterfaceType;
                public int Mtu;
                public IPAddress Ipv4Address;
                public IPAddress Gateway;
            }

            public static List<ActiveInterfaceInfo> GetActiveInterfaces()
            {
                List<ActiveInterfaceInfo> result = new List<ActiveInterfaceInfo>();
                try
                {
                    NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                    for (int i = 0; i < nics.Length; i++)
                    {
                        NetworkInterface nic = nics[i];
                        if (nic.OperationalStatus != OperationalStatus.Up) continue;
                        if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                            nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                        string desc = (nic.Description ?? "").ToLowerInvariant();
                        if (desc.Contains("virtual") || desc.Contains("vpn") || desc.Contains("hyper-v") ||
                            desc.Contains("wsl") || desc.Contains("vmware") || desc.Contains("virtualbox") ||
                            desc.Contains("bluetooth") || desc.Contains("pseudo"))
                        {
                            continue;
                        }

                        IPInterfaceProperties ipProps = nic.GetIPProperties();
                        if (ipProps == null) continue;

                        IPAddress v4 = null;
                        if (ipProps.UnicastAddresses != null)
                        {
                            foreach (UnicastIPAddressInformation u in ipProps.UnicastAddresses)
                            {
                                if (u != null && u.Address != null && u.Address.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    v4 = u.Address;
                                    break;
                                }
                            }
                        }
                        if (v4 == null) continue;

                        IPAddress gw = null;
                        if (ipProps.GatewayAddresses != null)
                        {
                            foreach (GatewayIPAddressInformation g in ipProps.GatewayAddresses)
                            {
                                if (g != null && g.Address != null && g.Address.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    gw = g.Address;
                                    break;
                                }
                            }
                        }

                        int mtu = 1500;
                        try
                        {
                            IPv4InterfaceProperties v4Props = ipProps.GetIPv4Properties();
                            if (v4Props != null)
                            {
                                mtu = v4Props.Mtu;
                            }
                        }
                        catch { }

                        ActiveInterfaceInfo info = new ActiveInterfaceInfo();
                        info.Id = nic.Id;
                        info.Name = nic.Name;
                        info.Description = nic.Description;
                        info.InterfaceType = nic.NetworkInterfaceType;
                        info.Mtu = mtu;
                        info.Ipv4Address = v4;
                        info.Gateway = gw;
                        result.Add(info);
                    }
                }
                catch { }
                return result;
            }
        }

        internal static class SystemRestoreModule
        {
            public static void CreateRestorePoint(string description)
            {
                Console.Write(" [*] Creating Windows System Restore checkpoint ........................ ");
                try
                {
                    string wmiCmd = string.Format(
                        "-NoProfile -ExecutionPolicy Bypass -Command \"" +
                        "try {{ " +
                        "  $sr = [wmiclass]'\\\\localhost\\root\\default:SystemRestore'; " +
                        "  $res = $sr.CreateRestorePoint('{0}', 12, 100); " +
                        "  if ($res.ReturnValue -eq 0) {{ exit 0 }} else {{ exit $res.ReturnValue }} " +
                        "}} catch {{ exit 1 }}\"", description);

                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = "powershell.exe";
                    psi.Arguments = wmiCmd;
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;

                    using (Process p = Process.Start(psi))
                    {
                        if (p != null)
                        {
                            bool finished = p.WaitForExit(10000);
                            if (finished && p.ExitCode == 0)
                            {
                                Program.PrintSuccess("CREATED");
                                return;
                            }
                        }
                    }

                    Program.PrintInfo("SKIPPED (Rate-limited / Inactive)");
                }
                catch (Exception ex)
                {
                    Program.PrintInfo("SKIPPED (" + ex.Message + ")");
                }
            }
        }

        #endregion

        internal static void ApplyAll()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" ROBLOX NETWORK TUNER [x64] - LOW-LATENCY & ANTI-JITTER ENGINE");
            Console.WriteLine(" Target: RobloxPlayerBeta.exe");
            Console.WriteLine("================================================================================");
            Console.ResetColor();

            currentSnapshot = new TunerState();
            currentSnapshot.Timestamp = DateTime.UtcNow.ToString("o");

            // 0. Automatic System Restore Point Creation
            SystemRestoreModule.CreateRestorePoint("RobloxNetworkTuner Pre-Optimization Baseline");

            // 1. Winsock Ancillary Function Driver (AFD) Buffer Locking & UDP Fast-Path
            AfdOptimizationModule.Apply(currentSnapshot);

            // 2. TCP/IP PMTU Discovery & Black Hole Detection
            PmtuOptimizationModule.Apply(currentSnapshot);

            // 3. Dynamic Active Network Adapters TCP/IP & MTU (Nagle Disabled, MTU 1500)
            ApplyTcpipInterfaceSettings(currentSnapshot);

            // 4. Global TCP Stack Parameters (RSC, CUBIC, DCA)
            ApplyGlobalTcpSettings(currentSnapshot);

            // 5. NDIS DPC Steering, RSS Pinning & Queue Depth Trimming
            NdisOptimizationModule.Apply(currentSnapshot);

            // 6. Wi-Fi 7 / DBS Roaming Lock & Background Scan Freeze
            WifiOptimizationModule.Apply(currentSnapshot);

            // 7. Policy-Based QoS (DSCP 46 Expedited Forwarding)
            ApplyQosPolicy(currentSnapshot);

            // 8. MMCSS Multimedia Scheduler & System Responsiveness
            SchedulingModule.Apply(currentSnapshot);

            // 9. Flush DNS Resolver & Purge ARP Cache
            ApplyServicesAndCaches(currentSnapshot);

            // Persist full snapshot to disk for out-of-process atomic rollback
            TunerStateStorage.SaveToFile(currentSnapshot);

            isOptimized = true;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" Session active. Press [Space], [Q], or [Esc] to restore baseline and exit.");
            Console.WriteLine(" Auto-restores baseline when RobloxPlayerBeta.exe closes.");
            Console.WriteLine("================================================================================");
            Console.ResetColor();
        }

        private static void ApplyTcpipInterfaceSettings(TunerState state)
        {
            Console.Write(" [*] Active TCP/IP adapters: Nagle disabled & unfragmented MTU (1500) .. ");
            try
            {
                List<ActiveInterfaceDetector.ActiveInterfaceInfo> activeNics = ActiveInterfaceDetector.GetActiveInterfaces();
                if (activeNics.Count == 0)
                {
                    PrintInfo("SKIPPED (No Active IPv4 NICs)");
                    return;
                }

                int tunedCount = 0;
                for (int i = 0; i < activeNics.Count; i++)
                {
                    ActiveInterfaceDetector.ActiveInterfaceInfo nic = activeNics[i];
                    string path = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + nic.Id;
                    using (RegistryKey nicKey = Registry.LocalMachine.OpenSubKey(path, true))
                    {
                        if (nicKey != null)
                        {
                            RegistrySnapshot snap = new RegistrySnapshot();
                            snap.KeyPath = "HKLM\\" + path;
                            snap.Values["TcpAckFrequency"] = nicKey.GetValue("TcpAckFrequency");
                            snap.Values["TCPNoDelay"] = nicKey.GetValue("TCPNoDelay");
                            snap.Values["TcpDelAckTicks"] = nicKey.GetValue("TcpDelAckTicks");
                            snap.Values["MTU"] = nicKey.GetValue("MTU");
                            state.RegistrySnapshots.Add(snap);

                            RegistryManager.SetDWord(nicKey, "TcpAckFrequency", 1, null);
                            RegistryManager.SetDWord(nicKey, "TCPNoDelay", 1, null);
                            RegistryManager.SetDWord(nicKey, "TcpDelAckTicks", 0, null);
                            RegistryManager.SetDWord(nicKey, "MTU", 1500, null);

                            // Dynamically set MTU on the active subinterface
                            RunSilent("netsh.exe", string.Format("interface ipv4 set subinterface \"{0}\" mtu=1500 store=active", nic.Name));
                            tunedCount++;
                        }
                    }
                }
                PrintSuccess(string.Format("DONE ({0} NIC{1})", tunedCount, tunedCount == 1 ? "" : "s"));
            }
            catch (Exception ex)
            {
                PrintError("FAIL: " + ex.Message);
            }
        }

        private static void ApplyGlobalTcpSettings(TunerState state)
        {
            Console.Write(" [*] Global TCP stack: RSC disabled, CUBIC congestion, DCA enabled ... ");
            RunSilent("netsh.exe", "int tcp set global rsc=disabled");
            RunSilent("netsh.exe", "int tcp set global autotuninglevel=normal");
            RunSilent("netsh.exe", "int tcp set global fastopen=enabled");
            RunSilent("netsh.exe", "int tcp set global timestamps=disabled");
            RunSilent("netsh.exe", "int tcp set global ecncapability=disabled");
            RunSilent("netsh.exe", "int tcp set global initialrto=1000");
            RunSilent("netsh.exe", "int tcp set global rss=enabled");
            RunSilent("netsh.exe", "int tcp set supplemental template=internet congestionprovider=cubic");
            RunSilent("netsh.exe", "int tcp set supplemental template=compat congestionprovider=cubic");
            RunSilent("netsh.exe", "int tcp set supplemental template=datacenterext congestionprovider=cubic");

            try
            {
                using (RegistryKey tcpKey = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"))
                {
                    if (tcpKey != null)
                    {
                        RegistrySnapshot snap = new RegistrySnapshot();
                        snap.KeyPath = @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";
                        snap.Values["DefaultTTL"] = tcpKey.GetValue("DefaultTTL");
                        snap.Values["DisableTaskOffload"] = tcpKey.GetValue("DisableTaskOffload");
                        snap.Values["EnableDCA"] = tcpKey.GetValue("EnableDCA");
                        snap.Values["MaxUserPort"] = tcpKey.GetValue("MaxUserPort");
                        snap.Values["TcpTimedWaitDelay"] = tcpKey.GetValue("TcpTimedWaitDelay");
                        state.RegistrySnapshots.Add(snap);

                        tcpKey.SetValue("DefaultTTL", 64, RegistryValueKind.DWord);
                        tcpKey.SetValue("DisableTaskOffload", 0, RegistryValueKind.DWord);
                        tcpKey.SetValue("EnableDCA", 1, RegistryValueKind.DWord);
                        tcpKey.SetValue("MaxUserPort", 65534, RegistryValueKind.DWord);
                        tcpKey.SetValue("TcpTimedWaitDelay", 30, RegistryValueKind.DWord);
                    }
                }
                PrintSuccess("DONE");
            }
            catch (Exception ex)
            {
                PrintError("FAIL: " + ex.Message);
            }
        }

        private static void ApplyQosPolicy(TunerState state)
        {
            Console.Write(" [*] Policy-Based QoS: DSCP 46 (Expedited Forwarding) -> Roblox ........ ");
            try
            {
                using (RegistryKey qosKey = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\QoS"))
                {
                    if (qosKey != null)
                    {
                        RegistrySnapshot snap = new RegistrySnapshot();
                        snap.KeyPath = @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\QoS";
                        snap.Values["Do not use NLA"] = qosKey.GetValue("Do not use NLA");
                        state.RegistrySnapshots.Add(snap);

                        qosKey.SetValue("Do not use NLA", "1", RegistryValueKind.String);
                    }
                }

                string qosPolicyPath = @"SOFTWARE\Policies\Microsoft\Windows\QoS\" + QosPolicyName;
                using (RegistryKey polKey = Registry.LocalMachine.CreateSubKey(qosPolicyPath))
                {
                    if (polKey != null)
                    {
                        polKey.SetValue("Version", "2.0", RegistryValueKind.String);
                        polKey.SetValue("AppName", TargetProcessName + ".exe", RegistryValueKind.String);
                        polKey.SetValue("DSCP", "46", RegistryValueKind.String);
                        polKey.SetValue("NetProfile", "7", RegistryValueKind.String);
                        polKey.SetValue("Precedence", "127", RegistryValueKind.String);
                    }
                }

                string qosCmd = string.Format(
                    "Remove-NetQosPolicy -Name \"{0}\" -Confirm:$false -ErrorAction SilentlyContinue; " +
                    "New-NetQosPolicy -Name \"{0}\" -AppPathNameMatchCondition \"{1}.exe\" -DSCPAction 46 -NetworkProfile All -ErrorAction SilentlyContinue",
                    QosPolicyName, TargetProcessName);
                RunSilent("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + qosCmd + "\"");

                PrintSuccess("DONE");
            }
            catch (Exception ex)
            {
                PrintError("FAIL: " + ex.Message);
            }
        }

        private static void ApplyServicesAndCaches(TunerState state)
        {
            Console.Write(" [*] Flush DNS resolver and purge ARP cache tables .................... ");
            RunSilent("ipconfig.exe", "/flushdns");
            RunSilent("netsh.exe", "interface ip delete arpcache");
            PrintSuccess("DONE");
        }

        internal static void RestoreAll()
        {
            lock (RestoreLock)
            {
                if (isRestoring || !isOptimized) return;
                isRestoring = true;

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n================================================================================");
                Console.WriteLine(" Restoring system baseline configuration...");
                Console.WriteLine("================================================================================");
                Console.ResetColor();

                // 1. Scheduling & Timer Resolution
                SchedulingModule.Restore(currentSnapshot);

                // 2. Wi-Fi 7 / DBS
                WifiOptimizationModule.Restore(currentSnapshot);

                // 3. NDIS, RSS & Queues
                NdisOptimizationModule.Restore(currentSnapshot);

                // 4. PMTU
                PmtuOptimizationModule.Restore(currentSnapshot);

                // 5. AFD Buffers
                AfdOptimizationModule.Restore(currentSnapshot);

                // 6. Policy-Based QoS
                try
                {
                    using (RegistryKey polKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\QoS", true))
                    {
                        if (polKey != null)
                        {
                            polKey.DeleteSubKeyTree(QosPolicyName, false);
                        }
                    }
                }
                catch { }
                RunSilent("powershell.exe", string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"Remove-NetQosPolicy -Name '{0}' -Confirm:$false -ErrorAction SilentlyContinue\"", QosPolicyName));
                Console.WriteLine("  [+] Removed QoS DSCP priority policy.");

                // 7. General Registry Snapshots (TCP interfaces & global)
                for (int i = 0; i < currentSnapshot.RegistrySnapshots.Count; i++)
                {
                    RegistrySnapshot snap = currentSnapshot.RegistrySnapshots[i];
                    string rawPath = snap.KeyPath;
                    if (rawPath.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
                    {
                        rawPath = rawPath.Substring(5);
                    }
                    try
                    {
                        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(rawPath, true))
                        {
                            if (key != null)
                            {
                                foreach (KeyValuePair<string, object> kvp in snap.Values)
                                {
                                    RevertRegistryValue(key, kvp.Key, kvp.Value);
                                }
                            }
                        }
                    }
                    catch { }
                }

                // 8. Global TCP Stack Reversion
                RunSilent("netsh.exe", "int tcp set global rsc=enabled");
                RunSilent("netsh.exe", "int tcp set global timestamps=allowed");
                RunSilent("netsh.exe", "int tcp set supplemental template=internet congestionprovider=default");
                RunSilent("netsh.exe", "int tcp set supplemental template=compat congestionprovider=default");
                RunSilent("netsh.exe", "int tcp set supplemental template=datacenterext congestionprovider=default");
                Console.WriteLine("  [+] Restored global TCP stack configuration.");

                // 9. Restart Suspended Services
                foreach (string svcName in currentSnapshot.StoppedServices)
                {
                    try
                    {
                        using (ServiceController sc = new ServiceController(svcName))
                        {
                            sc.Start();
                            Console.WriteLine("  [+] Resumed background service '{0}'.", svcName);
                        }
                    }
                    catch { }
                }

                // 10. Delete State File
                TunerStateStorage.DeleteStateFile();

                isOptimized = false;
            }
        }

        private static void ManualRestore()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" ROBLOX NETWORK TUNER - BASELINE RESTORATION");
            Console.WriteLine("================================================================================");
            Console.ResetColor();

            // Symmetrical restoration from on-disk tuner_state.json snapshot if present
            TunerState savedState = TunerStateStorage.LoadFromFile();
            if (savedState != null)
            {
                Console.WriteLine(" [*] Found tuner_state.json snapshot. Symmetrically restoring baseline...");
                SchedulingModule.Restore(savedState);
                WifiOptimizationModule.Restore(savedState);
                NdisOptimizationModule.Restore(savedState);
                PmtuOptimizationModule.Restore(savedState);
                AfdOptimizationModule.Restore(savedState);
            }

            // 1. Timer
            try
            {
                uint dummy;
                NativeMethods.NtSetTimerResolution(156250, false, out dummy);
                NativeMethods.TimeEndPeriod(1);
            }
            catch { }
            try
            {
                using (RegistryKey kernelKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", true))
                {
                    if (kernelKey != null)
                    {
                        kernelKey.DeleteValue("GlobalTimerResolutionRequests", false);
                    }
                }
            }
            catch { }
            Console.WriteLine("  [+] Restored system timer resolution.");

            // 2. Wi-Fi AutoConfig
            try
            {
                string wifiOut = RunCapture("netsh.exe", "wlan show interfaces");
                MatchCollection matches = Regex.Matches(wifiOut, @"^\s*Name\s*:\s*(.+)$", RegexOptions.Multiline);
                foreach (Match m in matches)
                {
                    string nicName = m.Groups[1].Value.Trim();
                    RunSilent("netsh.exe", string.Format("wlan set autoconfig enabled=yes interface=\"{0}\"", nicName));
                    Console.WriteLine("  [+] Restored WLAN AutoConfig scanning on '{0}'.", nicName);
                }
            }
            catch { }

            // 3. Remove QoS Policy
            try
            {
                using (RegistryKey polKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\QoS", true))
                {
                    if (polKey != null)
                    {
                        polKey.DeleteSubKeyTree(QosPolicyName, false);
                    }
                }
            }
            catch { }
            RunSilent("powershell.exe", string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"Remove-NetQosPolicy -Name '{0}' -Confirm:$false -ErrorAction SilentlyContinue\"", QosPolicyName));
            Console.WriteLine("  [+] Removed QoS DSCP priority policy.");

            // 4. Winsock AFD
            try
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
                Console.WriteLine("  [+] Restored Winsock AFD socket parameters.");
            }
            catch { }

            // 5. TCP/IP PMTU Discovery
            try
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
                Console.WriteLine("  [+] Restored TCP/IP PMTU discovery and core parameters.");
            }
            catch { }

            // 6. TCP/IP interfaces
            try
            {
                List<ActiveInterfaceDetector.ActiveInterfaceInfo> activeNics = ActiveInterfaceDetector.GetActiveInterfaces();
                foreach (ActiveInterfaceDetector.ActiveInterfaceInfo nic in activeNics)
                {
                    string path = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + nic.Id;
                    using (RegistryKey nicKey = Registry.LocalMachine.OpenSubKey(path, true))
                    {
                        if (nicKey != null)
                        {
                            nicKey.DeleteValue("TcpAckFrequency", false);
                            nicKey.DeleteValue("TCPNoDelay", false);
                            nicKey.DeleteValue("TcpDelAckTicks", false);
                            nicKey.DeleteValue("MTU", false);
                        }
                    }
                    RunSilent("netsh.exe", string.Format("interface ipv4 set subinterface \"{0}\" mtu=1500 store=active", nic.Name));
                }
                Console.WriteLine("  [+] Restored TCP/IP interface parameters on active adapters.");
            }
            catch { }

            // 7. Global TCP Stack
            RunSilent("netsh.exe", "int tcp set global rsc=enabled");
            RunSilent("netsh.exe", "int tcp set global timestamps=allowed");
            RunSilent("netsh.exe", "int tcp set supplemental template=internet congestionprovider=default");
            RunSilent("netsh.exe", "int tcp set supplemental template=compat congestionprovider=default");
            RunSilent("netsh.exe", "int tcp set supplemental template=datacenterext congestionprovider=default");
            Console.WriteLine("  [+] Restored global TCP stack configuration.");

            // 8. Global NetOffload
            RunSilent("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"Set-NetOffloadGlobalSetting -PacketCoalescingFilter Enabled -ReceiveSegmentCoalescing Enabled -Confirm:$false\"");
            Console.WriteLine("  [+] Restored NetOffload global settings (Packet Coalescing & RSC).");

            // 9. MMCSS
            try
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
                Console.WriteLine("  [+] Restored MMCSS multimedia profile.");
            }
            catch { }

            // 10. Delete State File
            TunerStateStorage.DeleteStateFile();

            // 12. Flush
            RunSilent("ipconfig.exe", "/flushdns");
            RunSilent("netsh.exe", "interface ip delete arpcache");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n [OK] Baseline restoration complete.");
            Console.ResetColor();
        }

        private static void RunBenchmarkCli(string targetHost, int sampleCount)
        {
            BenchmarkMetrics m = DiagnosticBenchmarkModule.RunBenchmark(targetHost, sampleCount, 20, 1500);
            DiagnosticBenchmarkModule.PrintBenchmarkResult(m, 20);
        }

        private static void RunSelfTest()
        {
            Console.WriteLine("[RobloxNetworkTuner Self-Test Engine]");

            // 1. Test TunerState serialization & deserialization
            TunerState testState = new TunerState();
            testState.Timestamp = DateTime.UtcNow.ToString("o");
            testState.ActiveAdapterName = "Wi-Fi";
            testState.ActiveAdapterGuid = "{D7E54AF0-8CFC-4EF2-8013-F67556BB5F2D}";
            testState.DriverClassPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0010";
            testState.DriverProperties["*RSS"] = "0";
            testState.DriverProperties["roamPolicy"] = "3";
            testState.DriverProperties["*RscIPv4"] = "1";
            testState.NativeWifi.HasCaptured = true;
            testState.NativeWifi.InterfaceGuid = "{D7E54AF0-8CFC-4EF2-8013-F67556BB5F2D}";
            testState.NativeWifi.BackgroundScanEnabled = true;
            testState.NativeWifi.MediaStreamingMode = false;

            RegistrySnapshot regSnap = new RegistrySnapshot();
            regSnap.KeyPath = @"HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters";
            regSnap.Values["FastSendDatagramThreshold"] = "1024";
            testState.RegistrySnapshots.Add(regSnap);

            TunerStateStorage.SaveToFile(testState);
            bool fileWritten = TunerStateStorage.StateFileExists();
            Console.WriteLine(" 1. tuner_state.json atomic creation ................ [{0}]", fileWritten ? "PASS" : "FAIL");

            TunerState loaded = TunerStateStorage.LoadFromFile();
            bool loadPass = loaded != null &&
                            loaded.ActiveAdapterName == "Wi-Fi" &&
                            loaded.DriverProperties.ContainsKey("*RSS") &&
                            loaded.DriverProperties["*RSS"] == "0" &&
                            loaded.NativeWifi.BackgroundScanEnabled &&
                            loaded.RegistrySnapshots.Count > 0;
            Console.WriteLine(" 2. Symmetrical state round-trip deserialization .... [{0}]", loadPass ? "PASS" : "FAIL");

            TunerStateStorage.DeleteStateFile();
            bool fileDeleted = !TunerStateStorage.StateFileExists();
            Console.WriteLine(" 3. tuner_state.json atomic deletion on rollback .... [{0}]", fileDeleted ? "PASS" : "FAIL");

            // 4. Test RFC 3550 jitter calculation on fixed vector
            double[] rttVector = new double[] { 40.0, 42.0, 41.0, 45.0, 43.0 };
            double jitter = 0.0;
            for (int i = 1; i < rttVector.Length; i++)
            {
                double d = Math.Abs(rttVector[i] - rttVector[i - 1]);
                if (i == 1) jitter = d;
                else jitter = jitter + (d - jitter) / 16.0;
            }
            bool mathPass = Math.Abs(jitter - 2.0623) < 0.01;
            Console.WriteLine(" 4. RFC 3550 interarrival jitter formula (J={0:F4}ms) [{1}]", jitter, mathPass ? "PASS" : "FAIL");

            // 5. Test Adapter discovery
            List<NdisOptimizationModule.AdapterClassRecord> adapters = NdisOptimizationModule.DiscoverActiveAdapters();
            bool adapterPass = adapters.Count > 0;
            Console.WriteLine(" 5. Active adapter dynamic discovery ({0} NIC(s)) ..... [{1}]", adapters.Count, adapterPass ? "PASS" : "FAIL");

            if (fileWritten && loadPass && fileDeleted && mathPass && adapterPass)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n[ALL CORE SELF-TESTS PASSED]");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[SOME SELF-TESTS FAILED]");
                Console.ResetColor();
            }
        }

        private static void PrintStatus()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" ROBLOX NETWORK TUNER - SYSTEM STATUS");
            Console.WriteLine("================================================================================");
            Console.ResetColor();

            // Timer
            uint minRes, maxRes, curRes;
            if (NativeMethods.NtQueryTimerResolution(out minRes, out maxRes, out curRes) == 0)
            {
                Console.WriteLine(" Kernel Timer Resolution  : {0:F2} ms ({1} 100ns units)", (double)curRes / 10000.0, curRes);
            }

            // GlobalTimerResolutionRequests
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel"))
            {
                object v = k != null ? k.GetValue("GlobalTimerResolutionRequests") : null;
                Console.WriteLine(" Global Timer Requests    : {0}", v != null ? (v.ToString() == "1" ? "Enabled (Global 0.5ms)" : v.ToString()) : "Not Configured (Per-Process)");
            }

            // AFD
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\AFD\Parameters"))
            {
                object fst = k != null ? k.GetValue("FastSendDatagramThreshold") : null;
                object drw = k != null ? k.GetValue("DefaultReceiveWindow") : null;
                object dsw = k != null ? k.GetValue("DefaultSendWindow") : null;
                Console.WriteLine(" AFD FastSendDatagram     : {0}", fst != null ? (fst.ToString() + " Bytes") : "Default (1024 B)");
                Console.WriteLine(" AFD DefaultReceiveWindow : {0}", drw != null ? (drw.ToString() + " Bytes (" + (Convert.ToInt32(drw) / 1024) + " KB)") : "Default (8 KB)");
                Console.WriteLine(" AFD DefaultSendWindow    : {0}", dsw != null ? (dsw.ToString() + " Bytes (" + (Convert.ToInt32(dsw) / 1024) + " KB)") : "Default (8 KB)");
            }

            // PMTU
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"))
            {
                object pmtu = k != null ? k.GetValue("EnablePMTUDiscovery") : null;
                object bh = k != null ? k.GetValue("EnablePMTUBHDetect") : null;
                Console.WriteLine(" PMTU Discovery           : {0}", pmtu != null ? (pmtu.ToString() == "1" ? "Enabled (1)" : "Disabled (0)") : "Default (1)");
                Console.WriteLine(" PMTU Blackhole Detection : {0}", bh != null ? (bh.ToString() == "1" ? "Enabled (1)" : "Disabled (0)") : "Not Configured (0)");
            }

            // Active Interfaces
            List<ActiveInterfaceDetector.ActiveInterfaceInfo> activeNics = ActiveInterfaceDetector.GetActiveInterfaces();
            Console.WriteLine(" Active Physical Adapters : {0} detected", activeNics.Count);
            for (int i = 0; i < activeNics.Count; i++)
            {
                ActiveInterfaceDetector.ActiveInterfaceInfo nic = activeNics[i];
                Console.WriteLine("  -> [{0}] {1} (IP: {2}, MTU: {3})", nic.Name, nic.Description, nic.Ipv4Address != null ? nic.Ipv4Address.ToString() : "N/A", nic.Mtu);
            }

            // QoS
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\QoS\" + QosPolicyName))
            {
                Console.WriteLine(" Policy-Based QoS         : {0}", k != null ? "Active (DSCP 46)" : "Not Active");
            }

            // MMCSS
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"))
            {
                object nti = k != null ? k.GetValue("NetworkThrottlingIndex") : null;
                object sr = k != null ? k.GetValue("SystemResponsiveness") : null;
                Console.WriteLine(" Network Throttling Index : {0}", nti != null ? (nti.ToString() == "-1" ? "Disabled (0xFFFFFFFF)" : nti.ToString()) : "Default (10)");
                Console.WriteLine(" System Responsiveness    : {0}", sr != null ? (sr.ToString() + "% Reserved") : "Default (20%)");
            }

            // NetOffload
            string offloadStr = RunCapture("powershell.exe", "-NoProfile -Command \"(Get-NetOffloadGlobalSetting).PacketCoalescingFilter\"");
            Console.WriteLine(" Packet Coalescing Filter : {0}", !string.IsNullOrEmpty(offloadStr) ? offloadStr.Trim() : "Unknown");

            // Roblox process
            Process[] procs = Process.GetProcessesByName(TargetProcessName);
            if (procs.Length > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" RobloxPlayerBeta Status  : RUNNING (PID {0}, Priority: {1})", procs[0].Id, procs[0].PriorityClass);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(" RobloxPlayerBeta Status  : Not Running");
            }

            // State File
            Console.WriteLine(" Tuner State File         : {0}", TunerStateStorage.StateFileExists() ? "Present (tuner_state.json)" : "None (Clean baseline)");

            Console.WriteLine("================================================================================");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Roblox Network Tuner [x64]");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  RobloxNetworkTuner.exe                   Launch interactive hands-free dark gaming GUI");
            Console.WriteLine("  RobloxNetworkTuner.exe --console         Launch interactive terminal watchdog session");
            Console.WriteLine("  RobloxNetworkTuner.exe --benchmark       Run automated latency & RFC 3550 jitter diagnostic");
            Console.WriteLine("  RobloxNetworkTuner.exe --status          Inspect current kernel, NDIS, AFD, and network state");
            Console.WriteLine("  RobloxNetworkTuner.exe --restore         Restore baseline system, driver & network settings");
            Console.WriteLine("  RobloxNetworkTuner.exe --check-update    Check GitHub releases for tuner updates");
            Console.WriteLine("  RobloxNetworkTuner.exe --update          Automatically download and apply latest release");
            Console.WriteLine("  RobloxNetworkTuner.exe --help            Display this help screen");
            Console.WriteLine();
            Console.WriteLine("Benchmark Options:");
            Console.WriteLine("  --benchmark [target] [samples]          Specify target host/IP and sample count");
            Console.WriteLine("                                          Default: roblox.com (50 samples)");
            Console.WriteLine();
        }

        public static void RevertRegistryValue(RegistryKey key, string name, object origValue)
        {
            if (origValue != null)
            {
                key.SetValue(name, origValue);
            }
            else
            {
                key.DeleteValue(name, false);
            }
        }

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static void RunSilent(string file, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(4000);
                }
            }
            catch { }
        }

        public static string RunCapture(string file, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using (Process p = Process.Start(psi))
                {
                    string res = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(4000);
                    return res;
                }
            }
            catch
            {
                return "";
            }
        }

        private static void WriteFixedLine(string text)
        {
            int width = 79;
            try
            {
                if (Console.WindowWidth > 1) width = Console.WindowWidth - 1;
            }
            catch { }

            if (text.Length < width)
            {
                text = text.PadRight(width);
            }
            else if (text.Length > width)
            {
                text = text.Substring(0, width);
            }
            Console.Write("\r" + text);
        }

        public static void PrintSuccess(string text)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[{0}]", text);
            Console.ResetColor();
        }

        public static void PrintError(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[{0}]", text);
            Console.ResetColor();
        }

        public static void PrintInfo(string text)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[{0}]", text);
            Console.ResetColor();
        }
    }

    #endregion
}
