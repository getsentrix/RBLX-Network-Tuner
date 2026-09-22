================================================================================
ROBLOX NETWORK TUNER [x64] - QUICK START GUIDE (v2.5.1)
================================================================================

Roblox Network Tuner is an evidence-based, adaptive low-latency optimization
and diagnostic engine engineered for Roblox on Windows 10 and Windows 11.

CORE CAPABILITIES & PILLARS:
- Adaptive Profile Tuning: Detects Wi-Fi vs. Ethernet. Ethernet connections
  skip wireless commands and receive low-latency NDIS queue steering.
- Real Roblox Server Telemetry: Tails active client transport logs in real time
  to ping the exact connected game server (UDMUX/RCC) rather than web endpoints.
- Bufferbloat Diagnostic Engine: Measures idle vs. loaded RTT under multi-stream
  contention, isolates local router vs. ISP queueing, and provides SQM advice.
- Multi-Factor Wi-Fi Roaming Guard: Monitors signal, packet loss, and jitter.
  Disengages scan locks when link degrades to allow seamless AP roaming.
- Evidence-Based QoS Verification: Verifies DSCP 46 Expedited Forwarding against
  ISP deprioritization or packet loss; reverts automatically if degraded.
- Crash-Resilient Architecture: Detects orphaned session states on startup and
  reverts to stock defaults. Includes --verify-restore audit command.
- AFD Fast-Path & 0.50 ms Timer: Sets Winsock datagram buffer thresholds for MTU
  frames and requests 0.50 ms (2000 Hz) NT kernel thread scheduling resolution.
- Priority & Power Boost: High process priority, High I/O priority, and
  explicit bypass of Windows 11 EcoQoS / Power Throttling.

HOW TO USE:
1. Launch "Roblox Network Tuner" from your Start Menu or Desktop.
2. Accept the Windows UAC elevation prompt (Administrator privileges required).
3. The engine activates adaptive optimizations and monitors for Roblox.
4. Join any Roblox game: the tuner tracks your real game server in real time.
5. When you close Roblox or click [Reset & Exit], all system settings revert
   to Windows defaults with 100% fidelity.

COMMAND LINE OPTIONS:
  RobloxNetworkTuner.exe                   Launch graphical dashboard
  RobloxNetworkTuner.exe --status          Display network, adapter, and session state
  RobloxNetworkTuner.exe --benchmark       Run RFC 3550 latency and jitter test
  RobloxNetworkTuner.exe --bufferbloat     Run loaded vs idle bufferbloat diagnostic
  RobloxNetworkTuner.exe --verify-restore  Audit and verify all settings match stock defaults
  RobloxNetworkTuner.exe --restore         Restore default Windows network settings
  RobloxNetworkTuner.exe --check-update    Check for updates on GitHub
  RobloxNetworkTuner.exe --update          Download and apply latest update
  RobloxNetworkTuner.exe --help            Show usage options

EMERGENCY RESET:
If your computer abruptly crashed or lost power during a session, simply launch
Roblox Network Tuner or run:
  RobloxNetworkTuner.exe --restore
The engine automatically detects and recovers orphaned configurations.

UNINSTALLATION:
Uninstall via Windows Settings -> Apps -> Installed Apps -> Roblox Network Tuner.
================================================================================
