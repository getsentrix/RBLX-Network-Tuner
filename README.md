# Roblox Low-Latency Network Tuner

High-performance Windows network and kernel latency tuner for Roblox. Targets UDP datagram paths, kernel timer quantization, process I/O scheduling, WLAN background scanning, and DSCP packet prioritization.

---

## Technical Architecture

### 1. Winsock AFD UDP Fast Path & Buffer Thresholds
* **Registry Key**: `HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters`
* **Settings**:
  * `FastSendDatagramThreshold` = `1500` (Routes UDP packets up to MTU size directly onto the kernel fast I/O path instead of buffering)
  * `FastCopyReceiveThreshold` = `1500` (Bypasses multi-stage socket receive copies for datagrams up to MTU)
  * `DoNotDisableReceiveBuffering` = `1` & `DoNotDisableSendBuffering` = `1` (Guarantees socket buffer allocation under heavy replication bursts)
  * `NonBlockingSendLimits` = `16` (Prevents datagram queue bloat in AFD)

### 2. Global NT Kernel Timer & Interrupt Resolution (0.50 ms)
* **APIs & Registry**:
  * `ntdll.dll!NtSetTimerResolution(5000, true)` & `winmm.dll!timeBeginPeriod(1)`
  * `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\kernel\GlobalTimerResolutionRequests` = `1`
* **Effect**: Tightens the Windows hardware interrupt timer from default 15.625 ms (64 Hz) down to 0.50 ms (2000 Hz). The `GlobalTimerResolutionRequests` registry key ensures 0.50 ms resolution applies globally across processes on Windows 10 (2004+) and Windows 11.

### 3. Process Execution, I/O Priority & Power Throttling Bypass
* **Target**: `RobloxPlayerBeta.exe`
* **Settings**:
  * CPU Scheduling: `HIGH_PRIORITY_CLASS` (0x80) via `kernel32.dll!SetPriorityClass` using scoped handle (`PROCESS_SET_INFORMATION`)
  * I/O Priority: `IoPriorityHigh` (3) via `ntdll.dll!NtSetInformationProcess` (Class 33)
  * Power Throttling: Explicitly disables Windows 11 EcoQoS / Efficiency Mode via `kernel32.dll!SetProcessInformation` (`PROCESS_POWER_THROTTLING_STATE`), preventing thread migration to low-power E-cores

### 4. Policy-Based QoS (DSCP 46 Expedited Forwarding) & NLA Bypass
* **Target**: `HKLM\SOFTWARE\Policies\Microsoft\Windows\QoS\RobloxPriority` & `HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\QoS`
* **Effect**: Tags all outgoing `RobloxPlayerBeta.exe` UDP and TCP packets with DSCP 46 (Expedited Forwarding, 802.11e WMM Voice queue). `Do not use NLA = "1"` ensures Windows NDIS applies DSCP tags on non-domain home networks.

### 5. Wi-Fi Background Roaming Scan Suppression
* **Target**: WLAN AutoConfig service (`netsh wlan set autoconfig enabled=no`)
* **Effect**: Windows scans for nearby Wi-Fi networks every 60 seconds. While the wireless adapter is off-channel, packet transmissions halt for 100–400 ms. Freezing autoconfig locks the radio to the active AP and eliminates periodic ping spikes.

### 6. TCP/IP Interface & Stack Optimization
* **Interface**: `HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{GUID}`
  * `TcpAckFrequency` = `1`, `TCPNoDelay` = `1`, `TcpDelAckTicks` = `0` (Disables Nagle buffering and delayed ACKs)
* **Core Stack**: `HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters`
  * `DefaultTTL` = `64`, `DisableTaskOffload` = `0` (hardware checksum offload), `EnableDCA` = `1` (Direct Cache Access to L3), `MaxUserPort` = `65534`, `TcpTimedWaitDelay` = `30`
* **Netsh Global**:
  * `rsc=disabled`, `fastopen=enabled`, `timestamps=disabled` (strips 12-byte header overhead), `ecncapability=disabled`, `initialrto=1000`, `rss=enabled`, `congestionprovider=cubic`

### 7. Multimedia Class Scheduler (MMCSS)
* **Registry Key**: `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile`
* **Settings**: `NetworkThrottlingIndex = 0xFFFFFFFF` (disables Windows network packet throttling), `SystemResponsiveness = 0` (100% CPU priority to foreground interactive game), `NoLazyMode = 1`, `AlwaysOn = 1`. Configures `Tasks\Games` with GPU Priority 8 and high SFIO priority.

### 8. Network Caches
* **Caches**: Flushes DNS resolver cache (`ipconfig /flushdns`) and purges stale ARP neighbor tables (`netsh interface ip delete arpcache`).

### 9. Statistical RFC 3550 Jitter & RTT Benchmark Engine
* **Benchmark Engine**: Implements an automated diagnostic tool that calculates Minimum, Maximum, Mean, and Median Round-Trip Time (RTT), Variance, Standard Deviation, and Packet Loss against edge servers.
* **RFC 3550 Interarrival Jitter**:
  $$D(i, j) = (R_j - R_i) - (S_j - S_i)$$
  $$J(i) = J(i-1) + \frac{|D(i-1, i)| - J(i-1)}{16}$$
* **Status Indication**: Evaluates whether network packet pacing achieves `Status: OPTIMIZED` (Jitter Variance < 2.0 ms²) or indicates unoptimized variance.

---

## Setup & Installation

The setup executable (`RobloxNetworkTunerSetup.exe`) is a standalone single-file installer:
* **Embedded Compressed Payload**: Embeds `RobloxNetworkTuner.pkg` (GZip compressed stream) to deliver the complete engine in a single binary.
* **Graphical Installer**: Clean dark-mode setup interface with progress display and step tracking.
* **Shell Integration**: Creates Desktop and Start Menu shortcuts, and registers in Windows Installed Apps (`Add/Remove Programs`).
* **Uninstaller Support**: Installs `uninstall.exe` which cleanly delegates to `%TEMP%` to remove all shortcuts, restore default network settings, and remove the installation folder.

---

## Usage

### Graphical Dashboard (Default)
Launch `RobloxNetworkTuner.exe` (or run via the Desktop shortcut):
1. Prompts for Administrator elevation (required for network stack and timer adjustments).
2. Displays active network interface configuration, Roblox process status, and live RFC 3550 edge latency and jitter metrics.
3. Automatically applies network and process priority optimizations.
4. When Roblox closes or the app exits, all system and network settings automatically revert to Windows defaults.
5. Supports minimizing to system tray for unobtrusive operation.

### Command Line Flags
```cmd
# Core Tuner Engine
RobloxNetworkTuner.exe               Launch graphical dashboard
RobloxNetworkTuner.exe --console     Launch console watchdog session
RobloxNetworkTuner.exe --status      Show current network and adapter configuration
RobloxNetworkTuner.exe --benchmark   Run RFC 3550 latency and jitter test
RobloxNetworkTuner.exe --restore     Restore default Windows network settings
RobloxNetworkTuner.exe --check-update Check for updates on GitHub
RobloxNetworkTuner.exe --update      Download and install latest release
RobloxNetworkTuner.exe --help        Display command line help

# Setup Installer
RobloxNetworkTunerSetup.exe          Run graphical installer
RobloxNetworkTunerSetup.exe -s       Run silent unattended installation
RobloxNetworkTunerSetup.exe -d <dir> Install to custom directory
RobloxNetworkTunerSetup.exe -u       Uninstall and restore default network settings
```

---

## Building from Source

To compile both the core engine and setup installer, run the build script in PowerShell:

```powershell
.\build_installer.ps1
```

Or compile manually using the 64-bit .NET Framework compiler (`csc.exe`):

```powershell
# 1. Compile Core Tuner Engine
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /out:RobloxNetworkTuner.exe /win32icon:app.ico /win32manifest:app.manifest /r:System.ServiceProcess.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /optimize+ /platform:x64 Program.cs

# 2. Compile Setup Installer
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /out:RobloxNetworkTunerSetup.exe /win32icon:app.ico /win32manifest:installer.manifest /res:RobloxNetworkTuner.pkg,RobloxNetworkTuner.pkg /r:System.ServiceProcess.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /optimize+ /platform:x64 Bootstrapper.cs
```
