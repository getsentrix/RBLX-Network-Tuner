================================================================================
ROBLOX NETWORK TUNER [x64] - QUICK START GUIDE
================================================================================

Roblox Network Tuner is a dedicated low-latency optimization tool
engineered for Roblox on Windows 10 and Windows 11.

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
1. Launch "Roblox Network Tuner" from your Start Menu or Desktop.
2. Accept the Windows UAC elevation prompt (Administrator privileges required).
3. The engine activates optimizations and waits for RobloxPlayerBeta.exe.
4. When you finish playing, press [Space], [Q], [Esc], or simply close Roblox.
   Settings automatically revert to Windows defaults when Roblox closes.

COMMAND LINE OPTIONS:
  RobloxNetworkTuner.exe              Launch graphical dashboard
  RobloxNetworkTuner.exe --status     Display current network and adapter state
  RobloxNetworkTuner.exe --benchmark  Run RFC 3550 latency and jitter test
  RobloxNetworkTuner.exe --restore    Restore default Windows network settings
  RobloxNetworkTuner.exe --help       Show usage options

EMERGENCY RESET:
If your computer abruptly shut down while running, launch "Reset Network Settings" from
the Start Menu, or run "Restore-Stock.bat" in the installation directory.

UNINSTALLATION:
Uninstall via Windows Settings -> Apps -> Installed Apps -> Roblox Network Tuner.
================================================================================
