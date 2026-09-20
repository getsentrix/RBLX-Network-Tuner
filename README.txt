================================================================================
ROBLOX NETWORK TUNER [x64] - QUICK START GUIDE
================================================================================

Roblox Network Tuner is a dedicated low-latency and anti-jitter optimization
engine engineered for Roblox on Windows 10 and Windows 11.

FEATURES:
- AFD Fast-Path: Locks Winsock datagram buffers to bypass socket queuing delays.
- Global 0.50 ms Timer: Sets NT kernel hardware interrupt timer to 0.50 ms (2000 Hz).
- Priority & Power Boost: Runs RobloxPlayerBeta.exe with HIGH_PRIORITY_CLASS,
  High I/O priority, and explicitly bypasses Windows 11 EcoQoS / Power Throttling.
- QoS DSCP 46 Tagging: Tags outgoing packets with Expedited Forwarding priority.
- Wi-Fi Scan Freeze: Locks WLAN radio to prevent 60-second background scan spikes.
- Nagle Disablement: Eliminates delayed ACKs and packet coalescing.
- Automated Jitter Benchmark: Measures packet pacing and jitter variance (RFC 3550).

HOW TO USE:
1. Double-click the "Roblox Network Tuner" shortcut on your Desktop or Start Menu.
2. Accept the Windows UAC elevation prompt (Administrator privileges required).
3. The engine activates all optimizations and waits for RobloxPlayerBeta.exe.
4. When you finish playing, press [Space], [Q], [Esc], or simply close Roblox.
   Your system configuration will automatically restore to baseline.

COMMAND LINE OPTIONS:
  RobloxNetworkTuner.exe              Launch interactive session
  RobloxNetworkTuner.exe --status     Display current kernel and network stack state
  RobloxNetworkTuner.exe --benchmark  Run RFC 3550 RTT and Jitter benchmark
  RobloxNetworkTuner.exe --restore    Perform manual restoration to system baseline
  RobloxNetworkTuner.exe --help       Show usage options

EMERGENCY RESTORE:
If your computer abruptly shut down while running, launch "Restore Baseline" from
the Start Menu, or run "Restore-Stock.bat" in the installation directory.

UNINSTALLATION:
You can uninstall Roblox Network Tuner at any time via:
- Windows Settings -> Apps -> Installed Apps -> Roblox Network Tuner -> Uninstall
- Or Control Panel -> Programs and Features -> Roblox Network Tuner -> Uninstall
================================================================================
