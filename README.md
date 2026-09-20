# Roblox Low-Latency Network Tuner

**Version 2.2.0** — High-performance, evidence-based, adaptive Windows network and kernel latency engine for Roblox.

---

## Overview

Roblox Network Tuner is built from the ground up to address competitive Roblox network performance, ping stability, and packet jitter. Rather than applying blind or static registry tweaks, the v2.2.0 engine implements **adaptive profile intelligence**, **real-time Roblox game server telemetry**, and an **evidence-based bufferbloat diagnostic suite**.

---

## The 6 Architectural Pillars

### 1. Adaptive Profile-Driven Tuning (Wi-Fi vs. Ethernet Awareness)
- **Primary Interface Discovery**: Detects the active physical default-gateway adapter and classifies the medium (`Ethernet` vs. `Wi-Fi`).
- **Ethernet Profiles**: Completely skips Wi-Fi AutoConfig manipulation and radio lock commands. Applies gigabit low-latency NDIS queue steering, Energy Efficient Ethernet (EEE) disablement, and flow control offloading.
- **Wi-Fi Profiles**: Evaluates signal quality (%) and RSSI (dBm). Binds to active WLAN interfaces dynamically rather than across virtual or disconnected adapters.

### 2. Real-Time Roblox Game Server Telemetry (`RobloxGameSessionTracker`)
- **Non-Locking Transport Log Tailer**: Opens `%LOCALAPPDATA%\Roblox\logs\*Player*.log` using non-locking `FileShare.ReadWrite` access.
- **Live Datacenter Extraction**: Parses live UDP multiplexer connection tokens (`UDMUX Address = <IP>, Port = <Port> | RCC Server Address = <IP> | Datacenter = <ID>`).
- **Actual Server Diagnostics**: All real-time telemetry, RTT gauges, RFC 3550 jitter calculators, and diagnostic pings target the **connected game server**, giving players genuine in-game network telemetry rather than artificial web server pings.

### 3. Bufferbloat Diagnostic Engine (`BufferbloatDiagnosticModule`)
- **Idle vs. Loaded Stress Benchmark**:
  - Phase 1: Measures baseline idle Round-Trip Time ($RTT_{idle}$, 10 samples).
  - Phase 2: Generates a concurrent 5MB network burst via edge CDN and measures loaded Round-Trip Time ($RTT_{loaded}$, 15 samples).
  - Phase 3: Computes latency delta $\Delta RTT = RTT_{loaded} - RTT_{idle}$ and assigns grades from **A+** down to **F**.
- **Honest Router Diagnosis**: If local network latency degrades under load ($\Delta RTT \ge 30\text{ ms}$), the tuner explicitly informs the user that the bottleneck is router queuebloat and provides guidance on router Smart Queue Management (SQM / CAKE / FQ-CoDel), avoiding false claims that OS registry tweaks can fix router queues.

### 4. Selective Signal-Aware Wi-Fi Optimization (`WifiOptimizationModule`)
- **Dynamic Link Assessment**: Queries WLAN link quality and RSSI continuously.
- **Dropout Prevention**: If Wi-Fi signal quality drops below **55%** or RSSI falls below **-75 dBm**, the engine automatically skips roaming scan suppression. This ensures the Wi-Fi adapter can roam to stronger access points or repeaters without connection dropouts.
- **Media Streaming Mode**: Activates WlanMediaStreamingMode when signal is strong ($\ge 55\%$) to minimize packet batching and DPC latency.

### 5. Evidence-Based QoS Verification (`QosVerificationModule`)
- **DSCP 46 Expedited Forwarding**: Tags outgoing `RobloxPlayerBeta.exe` UDP and TCP packets with DSCP 46 (802.11e WMM Voice Priority).
- **Automated ISP Audit**: Many consumer ISPs and residential routers deprioritize or drop packets bearing non-zero DSCP tags. The verification module measures packet loss and latency before and after policy application.
- **Auto-Revert**: If packet loss occurs or latency degrades with DSCP active, the policy is immediately removed and marked `SKIPPED (Deprioritized by Gateway/ISP)`.

### 6. Crash-Resilient Safe System Management (`CrashRecoveryModule`)
- **Atomic State Persistence**: Backs up all original driver properties, registry keys, and timer settings to an atomic `tuner_state.json` file before applying changes.
- **Orphan Session Recovery**: On startup, checks for leftover state from sudden power outages or system crashes, and automatically reverts all settings to stock Windows defaults.
- **Interface-Scoped Safety**: Scopes all TCP/IP and adapter optimizations strictly to the active adapter's GUID (`HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{GUID}`), leaving virtual and inactive adapters untouched.
- **Restoration Audit (`--verify-restore`)**: Verifies that 100% of modified registry keys, services, and kernel timers match default Windows configuration.

---

## Additional Low-Latency Mechanisms

### Winsock AFD UDP Fast Path & Buffer Thresholds
* **Registry Key**: `HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters`
* **Settings**:
  * `FastSendDatagramThreshold` = `1500` (Routes UDP packets up to MTU size directly onto the kernel fast I/O path instead of buffering)
  * `FastCopyReceiveThreshold` = `1500` (Bypasses multi-stage socket receive copies for datagrams up to MTU)
  * `DoNotDisableReceiveBuffering` = `1` & `DoNotDisableSendBuffering` = `1` (Guarantees socket buffer allocation under heavy replication bursts)
  * `NonBlockingSendLimits` = `16` (Prevents datagram queue bloat in AFD)

### Global NT Kernel Timer Resolution (0.50 ms)
* **APIs & Registry**:
  * `ntdll.dll!NtSetTimerResolution(5000, true)` & `winmm.dll!timeBeginPeriod(1)`
  * `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\kernel\GlobalTimerResolutionRequests` = `1`
* **Effect**: Tightens hardware interrupt timer from default 15.625 ms (64 Hz) down to 0.50 ms (2000 Hz) globally across all processes on Windows 10 (2004+) and Windows 11.

### Process Execution, I/O Priority & Power Throttling Bypass
* CPU Scheduling: `HIGH_PRIORITY_CLASS` (0x80) via `kernel32.dll!SetPriorityClass`.
* I/O Priority: `IoPriorityHigh` (3) via `ntdll.dll!NtSetInformationProcess`.
* Power Throttling: Explicitly disables Windows 11 EcoQoS / Efficiency Mode via `kernel32.dll!SetProcessInformation` (`PROCESS_POWER_THROTTLING_STATE`), preventing thread migration to low-power E-cores.

### Statistical Telemetry & RFC 3550 Interarrival Jitter
Calculates Minimum, Maximum, Mean, P50 (Median), P95, P99, and 95% Confidence Intervals ($\bar{x} \pm 1.96 \cdot \frac{s}{\sqrt{n}}$):
$$D(i, j) = (R_j - R_i) - (S_j - S_i)$$
$$J(i) = J(i-1) + \frac{|D(i-1, i)| - J(i-1)}{16}$$

---

## Usage

### Graphical Dashboard (Default)
Launch `RobloxNetworkTuner.exe`:
- Prompts for Administrator elevation (required for network stack and kernel timer adjustments).
- Displays active network interface card, connection medium, signal quality, and live Roblox game server IP/port.
- Displays live RTT, RFC 3550 jitter, and gradient latency gauge.
- Includes **[Bufferbloat Test]** button for instant network load testing.
- When Roblox closes or the app exits, all system and network settings automatically revert to Windows defaults.

### Command Line Interface
```cmd
# Core Engine
RobloxNetworkTuner.exe                   Launch graphical dashboard
RobloxNetworkTuner.exe --status          Display current network, adapter, and session state
RobloxNetworkTuner.exe --benchmark       Run RFC 3550 latency and jitter diagnostic
RobloxNetworkTuner.exe --bufferbloat     Run loaded vs. idle bufferbloat diagnostic
RobloxNetworkTuner.exe --verify-restore  Audit all settings against stock Windows defaults
RobloxNetworkTuner.exe --restore         Restore default Windows network settings
RobloxNetworkTuner.exe --check-update    Check for updates on GitHub
RobloxNetworkTuner.exe --update          Download and apply latest update
RobloxNetworkTuner.exe --help            Display help screen

# Setup Bootstrapper
RobloxNetworkTunerSetup.exe              Launch graphical installer
RobloxNetworkTunerSetup.exe -s           Run silent unattended installation
RobloxNetworkTunerSetup.exe -d <dir>     Install to custom directory
RobloxNetworkTunerSetup.exe -u           Uninstall and restore default network settings
```

---

## Building from Source

Compile the complete suite using the automated PowerShell build script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build_installer.ps1 -SkipSigning
```

Binaries and SHA-256 manifests are generated in the `release\` directory:
- `release\RobloxNetworkTuner.exe`
- `release\RobloxNetworkTunerSetup.exe`
- `release\SHA256SUMS.txt`
