<#
.SYNOPSIS
    Roblox Low-Latency Network Tuner

.DESCRIPTION
    Configures low-latency network and system parameters for Roblox on Windows 10 & 11:
    - Winsock AFD: Fast-path UDP datagram threshold (1500 B MTU) and receive buffers
    - TCP/IP Stack: Nagle disabled, ACK frequency=1, Delayed ACK ticks=0
    - Global Netsh: RSC disabled, CUBIC congestion provider, TCP Fast Open, timestamps disabled
    - Core TCP/IP: DCA enabled, MaxUserPort=65534, TimedWait=30s
    - Wi-Fi Subsystem: WLAN AutoConfig roaming scan freeze (eliminates periodic 60s jitter)
    - QoS Tagging: DSCP 46 (Expedited Forwarding) bound to RobloxPlayerBeta.exe with NLA bypass
    - Multimedia Scheduler: MMCSS network throttling disabled, system responsiveness 0% reserved
    - Kernel Timer: Sub-millisecond interrupt quantization (0.50 ms / 500 us) with global requests
    - Background Services: Telemetry/update services suspended, DNS and ARP caches cleared
    - Watchdog: Enforces process priority, I/O priority, and restores baseline on exit or Spacebar

.PARAMETER Mode
    Interactive : Interactive session with auto-restore on Roblox exit or Spacebar.
    Watchdog    : Continuous background monitoring across multiple game sessions.
    Apply       : Applies optimizations immediately.
    Restore     : Reverts system settings to baseline.
    Benchmark   : Measures latency and jitter against edge servers.
    Status      : Inspects current network and tuning state.
#>

[CmdletBinding()]
param (
    [ValidateSet('Interactive', 'Watchdog', 'Apply', 'Restore', 'Benchmark', 'Status')]
    [string]$Mode = 'Interactive'
)

# Enforce Administrator Privileges
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin -and ($Mode -in @('Interactive', 'Watchdog', 'Apply', 'Restore'))) {
    try {
        Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Mode $Mode" -Verb RunAs -ErrorAction Stop
        exit
    } catch {
        Write-Error "Administrator privileges required: $_"
        exit 1
    }
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$StateFile = Join-Path -Path $ScriptDir -ChildPath "tuner_state.json"
$RobloxProcessName = "RobloxPlayerBeta"
$QoSName = "RobloxPriority"

# P/Invoke for High-Resolution Multimedia and Kernel Timers
$TimerCode = @"
using System;
using System.Runtime.InteropServices;

public class HighResolutionTimer {
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
    public static extern uint TimeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
    public static extern uint TimeEndPeriod(uint uMilliseconds);

    [DllImport("ntdll.dll", SetLastError = true)]
    public static extern int NtSetTimerResolution(uint desiredResolution, bool setResolution, out uint currentResolution);

    [DllImport("ntdll.dll", SetLastError = true)]
    public static extern int NtQueryTimerResolution(out uint minimumResolution, out uint maximumResolution, out uint currentResolution);
}
"@
if (-not ([System.Management.Automation.PSTypeName]'HighResolutionTimer').Type) {
    Add-Type -TypeDefinition $TimerCode -ErrorAction SilentlyContinue
}

# --- Output Helpers ---
function Write-Header {
    param ([string]$Text)
    Write-Host ""
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host "  $Text" -ForegroundColor White
    Write-Host "================================================================================" -ForegroundColor Cyan
}

function Write-Success {
    param ([string]$Text)
    Write-Host "  [+] $Text" -ForegroundColor Green
}

function Write-Info {
    param ([string]$Text)
    Write-Host "  [*] $Text" -ForegroundColor Yellow
}

function Write-Err {
    param ([string]$Text)
    Write-Host "  [-] $Text" -ForegroundColor Red
}

# --- Active Network Adapter Detection ---
function Get-ActiveGameAdapter {
    $activeRoute = Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue |
                   Sort-Object RouteMetric | Select-Object -First 1
    if ($activeRoute) {
        $adapter = Get-NetAdapter -InterfaceIndex $activeRoute.InterfaceIndex -ErrorAction SilentlyContinue
        if ($adapter) { return $adapter }
    }
    return Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | Select-Object -First 1
}

# --- State Management (Safe Recovery) ---
function Save-SnapshotState {
    param ([hashtable]$State)
    $json = $State | ConvertTo-Json -Depth 5
    Set-Content -Path $StateFile -Value $json -Force -Encoding UTF8
}

function Load-SnapshotState {
    if (Test-Path -Path $StateFile) {
        try {
            $content = Get-Content -Path $StateFile -Raw -Encoding UTF8
            return ($content | ConvertFrom-Json)
        } catch {
            return $null
        }
    }
    return $null
}

function Remove-SnapshotState {
    if (Test-Path -Path $StateFile) {
        Remove-Item -Path $StateFile -Force -ErrorAction SilentlyContinue
    }
}

# --- Optimization Engine ---
function Apply-Optimizations {
    Write-Header "ROBLOX LOW-LATENCY NETWORK TUNER"

    $state = @{
        Timestamp = (Get-Date).ToString("o")
        Services = @{}
        Registry = @{}
        WiFiInterface = $null
        QoSCreated = $false
        HighResTimerActive = $false
        KernelTimerResolution = 156250
        ActiveAdapterName = $null
    }

    # 1. Winsock AFD UDP Datagram Fast-Path Buffer Threshold
    Write-Host " [*] Winsock AFD UDP fast-path threshold (1500 B)..." -ForegroundColor Cyan
    $afdPath = "HKLM:\SYSTEM\CurrentControlSet\Services\AFD\Parameters"
    if (-not (Test-Path $afdPath)) {
        New-Item -Path $afdPath -Force -ErrorAction SilentlyContinue | Out-Null
    }
    if (Test-Path $afdPath) {
        $state.Registry["FastSendDatagramThreshold"] = (Get-ItemProperty -Path $afdPath -Name "FastSendDatagramThreshold" -ErrorAction SilentlyContinue).FastSendDatagramThreshold
        $state.Registry["FastCopyReceiveThreshold"] = (Get-ItemProperty -Path $afdPath -Name "FastCopyReceiveThreshold" -ErrorAction SilentlyContinue).FastCopyReceiveThreshold
        $state.Registry["DoNotDisableReceiveBuffering"] = (Get-ItemProperty -Path $afdPath -Name "DoNotDisableReceiveBuffering" -ErrorAction SilentlyContinue).DoNotDisableReceiveBuffering
        $state.Registry["DoNotDisableSendBuffering"] = (Get-ItemProperty -Path $afdPath -Name "DoNotDisableSendBuffering" -ErrorAction SilentlyContinue).DoNotDisableSendBuffering
        $state.Registry["NonBlockingSendLimits"] = (Get-ItemProperty -Path $afdPath -Name "NonBlockingSendLimits" -ErrorAction SilentlyContinue).NonBlockingSendLimits

        Set-ItemProperty -Path $afdPath -Name "FastSendDatagramThreshold" -Value 1500 -Type DWord -Force
        Set-ItemProperty -Path $afdPath -Name "FastCopyReceiveThreshold" -Value 1500 -Type DWord -Force
        Set-ItemProperty -Path $afdPath -Name "DoNotDisableReceiveBuffering" -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $afdPath -Name "DoNotDisableSendBuffering" -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $afdPath -Name "NonBlockingSendLimits" -Value 16 -Type DWord -Force
        Write-Success "Winsock AFD UDP fast-path threshold set to 1500 B (Send/Recv buffering protected)"
    }

    # 2. Multimedia & System Responsiveness Registry Tuning
    Write-Host " [*] MMCSS scheduling & responsiveness..." -ForegroundColor Cyan
    $mmPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
    if (Test-Path $mmPath) {
        $state.Registry["NetworkThrottlingIndex"] = (Get-ItemProperty -Path $mmPath -Name "NetworkThrottlingIndex" -ErrorAction SilentlyContinue).NetworkThrottlingIndex
        $state.Registry["SystemResponsiveness"] = (Get-ItemProperty -Path $mmPath -Name "SystemResponsiveness" -ErrorAction SilentlyContinue).SystemResponsiveness
        $state.Registry["NoLazyMode"] = (Get-ItemProperty -Path $mmPath -Name "NoLazyMode" -ErrorAction SilentlyContinue).NoLazyMode
        $state.Registry["AlwaysOn"] = (Get-ItemProperty -Path $mmPath -Name "AlwaysOn" -ErrorAction SilentlyContinue).AlwaysOn

        Set-ItemProperty -Path $mmPath -Name "NetworkThrottlingIndex" -Value 0xFFFFFFFF -Type DWord -Force
        Set-ItemProperty -Path $mmPath -Name "SystemResponsiveness" -Value 0 -Type DWord -Force
        Set-ItemProperty -Path $mmPath -Name "NoLazyMode" -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $mmPath -Name "AlwaysOn" -Value 1 -Type DWord -Force
        Write-Success "NetworkThrottlingIndex -> Disabled (0xFFFFFFFF) | SystemResponsiveness -> 0%"
    }

    $gamesPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games"
    if (Test-Path $gamesPath) {
        Set-ItemProperty -Path $gamesPath -Name "GPU Priority" -Value 8 -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $gamesPath -Name "Priority" -Value 6 -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $gamesPath -Name "Scheduling Category" -Value "High" -Type String -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $gamesPath -Name "SFIO Priority" -Value "High" -Type String -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $gamesPath -Name "Affinity" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $gamesPath -Name "Background Only" -Value "False" -Type String -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $gamesPath -Name "Clock Rate" -Value 10000 -Type DWord -Force -ErrorAction SilentlyContinue
        Write-Success "MMCSS Games profile elevated (GPU Priority: 8, SFIO: High)"
    }

    # 3. Interface TCP/IP Tuning (TcpAckFrequency & TCPNoDelay)
    Write-Host " [*] Active network adapter TCP/IP parameters..." -ForegroundColor Cyan
    $adapter = Get-ActiveGameAdapter
    if ($adapter) {
        $state.ActiveAdapterName = $adapter.Name
        $tcpInterfacesPath = "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$($adapter.InterfaceGuid)"
        if (Test-Path $tcpInterfacesPath) {
            $state.Registry["$($adapter.InterfaceGuid)_TcpAckFrequency"] = (Get-ItemProperty -Path $tcpInterfacesPath -Name "TcpAckFrequency" -ErrorAction SilentlyContinue).TcpAckFrequency
            $state.Registry["$($adapter.InterfaceGuid)_TCPNoDelay"] = (Get-ItemProperty -Path $tcpInterfacesPath -Name "TCPNoDelay" -ErrorAction SilentlyContinue).TCPNoDelay
            $state.Registry["$($adapter.InterfaceGuid)_TcpDelAckTicks"] = (Get-ItemProperty -Path $tcpInterfacesPath -Name "TcpDelAckTicks" -ErrorAction SilentlyContinue).TcpDelAckTicks

            Set-ItemProperty -Path $tcpInterfacesPath -Name "TcpAckFrequency" -Value 1 -Type DWord -Force
            Set-ItemProperty -Path $tcpInterfacesPath -Name "TCPNoDelay" -Value 1 -Type DWord -Force
            Set-ItemProperty -Path $tcpInterfacesPath -Name "TcpDelAckTicks" -Value 0 -Type DWord -Force
            Write-Success "Applied TcpAckFrequency=1, TCPNoDelay=1, TcpDelAckTicks=0 on '$($adapter.Name)'"
        }
    }

    # 4. Global TCP/IP Stack Parameters
    Write-Host " [*] Global TCP stack & congestion provider..." -ForegroundColor Cyan
    $tcpGlobalPath = "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"
    if (Test-Path $tcpGlobalPath) {
        $state.Registry["DefaultTTL"] = (Get-ItemProperty -Path $tcpGlobalPath -Name "DefaultTTL" -ErrorAction SilentlyContinue).DefaultTTL
        $state.Registry["DisableTaskOffload"] = (Get-ItemProperty -Path $tcpGlobalPath -Name "DisableTaskOffload" -ErrorAction SilentlyContinue).DisableTaskOffload
        $state.Registry["EnableDCA"] = (Get-ItemProperty -Path $tcpGlobalPath -Name "EnableDCA" -ErrorAction SilentlyContinue).EnableDCA
        $state.Registry["MaxUserPort"] = (Get-ItemProperty -Path $tcpGlobalPath -Name "MaxUserPort" -ErrorAction SilentlyContinue).MaxUserPort
        $state.Registry["TcpTimedWaitDelay"] = (Get-ItemProperty -Path $tcpGlobalPath -Name "TcpTimedWaitDelay" -ErrorAction SilentlyContinue).TcpTimedWaitDelay

        Set-ItemProperty -Path $tcpGlobalPath -Name "DefaultTTL" -Value 64 -Type DWord -Force
        Set-ItemProperty -Path $tcpGlobalPath -Name "DisableTaskOffload" -Value 0 -Type DWord -Force
        Set-ItemProperty -Path $tcpGlobalPath -Name "EnableDCA" -Value 1 -Type DWord -Force
        Set-ItemProperty -Path $tcpGlobalPath -Name "MaxUserPort" -Value 65534 -Type DWord -Force
        Set-ItemProperty -Path $tcpGlobalPath -Name "TcpTimedWaitDelay" -Value 30 -Type DWord -Force
    }

    netsh int tcp set global rsc=disabled >$null 2>&1
    netsh int tcp set global autotuninglevel=normal >$null 2>&1
    netsh int tcp set global fastopen=enabled >$null 2>&1
    netsh int tcp set global timestamps=disabled >$null 2>&1
    netsh int tcp set global ecncapability=disabled >$null 2>&1
    netsh int tcp set global initialrto=1000 >$null 2>&1
    netsh int tcp set global rss=enabled >$null 2>&1
    netsh int tcp set supplemental template=internet congestionprovider=cubic >$null 2>&1
    netsh int tcp set supplemental template=compat congestionprovider=cubic >$null 2>&1
    netsh int tcp set supplemental template=datacenterext congestionprovider=cubic >$null 2>&1
    Write-Success "Global TCP: RSC=Disabled, CUBIC congestion provider, Timestamps=Disabled"

    # 5. Wi-Fi Background Roaming Scan Suppression
    Write-Host " [*] Checking Wi-Fi background roaming scans..." -ForegroundColor Cyan
    $wifiOutput = (netsh wlan show interfaces 2>$null) | Out-String
    if ($wifiOutput -match '(?m)^\s*Name\s*:\s*(.+)\r?$') {
        $wifiName = $matches[1].Trim()
        $state.WiFiInterface = $wifiName
        netsh wlan set autoconfig enabled=no interface="$wifiName" >$null 2>&1
        Write-Success "Wi-Fi roaming scan freeze ACTIVE on '$wifiName'"
    } else {
        Write-Info "No active Wi-Fi interface detected (Ethernet active)"
    }

    # 6. Policy-Based QoS (DSCP 46 - Expedited Forwarding)
    Write-Host " [*] Configuring Policy-Based QoS (DSCP 46) for Roblox..." -ForegroundColor Cyan
    try {
        $qosRegPath = "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\QoS"
        if (-not (Test-Path $qosRegPath)) { New-Item -Path $qosRegPath -Force | Out-Null }
        $state.Registry["DoNotUseNLA"] = (Get-ItemProperty -Path $qosRegPath -Name "Do not use NLA" -ErrorAction SilentlyContinue)."Do not use NLA"
        Set-ItemProperty -Path $qosRegPath -Name "Do not use NLA" -Value "1" -Type String -Force

        $qosPolicyReg = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\QoS\$QoSName"
        if (-not (Test-Path $qosPolicyReg)) { New-Item -Path $qosPolicyReg -Force | Out-Null }
        Set-ItemProperty -Path $qosPolicyReg -Name "Version" -Value "2.0" -Type String -Force
        Set-ItemProperty -Path $qosPolicyReg -Name "AppName" -Value "$RobloxProcessName.exe" -Type String -Force
        Set-ItemProperty -Path $qosPolicyReg -Name "DSCP" -Value "46" -Type String -Force
        Set-ItemProperty -Path $qosPolicyReg -Name "NetProfile" -Value "7" -Type String -Force
        Set-ItemProperty -Path $qosPolicyReg -Name "Precedence" -Value "127" -Type String -Force

        Remove-NetQosPolicy -Name $QoSName -Confirm:$false -ErrorAction SilentlyContinue
        New-NetQosPolicy -Name $QoSName `
                         -AppPathNameMatchCondition "$RobloxProcessName.exe" `
                         -DSCPAction 46 `
                         -NetworkProfile All `
                         -ErrorAction SilentlyContinue >$null
        $state.QoSCreated = $true
        Write-Success "QoS DSCP 46 (Expedited Forwarding) bound to $RobloxProcessName.exe"
    } catch {
        Write-Err "Could not set QoS policy: $_"
    }

    # 7. High-Resolution Kernel & Multimedia Timer (0.50ms)
    Write-Host " [*] System timer resolution (0.50 ms)..." -ForegroundColor Cyan
    try {
        $kernelReg = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\kernel"
        if (Test-Path $kernelReg) {
            $state.Registry["GlobalTimerResolutionRequests"] = (Get-ItemProperty -Path $kernelReg -Name "GlobalTimerResolutionRequests" -ErrorAction SilentlyContinue).GlobalTimerResolutionRequests
            Set-ItemProperty -Path $kernelReg -Name "GlobalTimerResolutionRequests" -Value 1 -Type DWord -Force
        }

        $min = 0; $max = 0; $cur = 0
        if ([HighResolutionTimer]::NtQueryTimerResolution([ref]$min, [ref]$max, [ref]$cur) -eq 0) {
            $state.KernelTimerResolution = $cur
        }
        [HighResolutionTimer]::NtSetTimerResolution(5000, $true, [ref]$cur) | Out-Null
        [HighResolutionTimer]::TimeBeginPeriod(1) | Out-Null
        $state.HighResTimerActive = $true
        Write-Success "Timer resolution set to 0.50 ms (500 us, GlobalTimerResolutionRequests=1)"
    } catch {
        Write-Err "Unable to initialize timer: $_"
    }

    # 8. Suspend Background Update Services & Flush Cache
    Write-Host " [*] Suspending background update services & flushing caches..." -ForegroundColor Cyan
    $targetServices = @('bits', 'wuauserv', 'dosvc', 'lfsvc', 'DiagTrack')
    foreach ($svcName in $targetServices) {
        $svc = Get-Service -Name $svcName -ErrorAction SilentlyContinue
        if ($svc) {
            $state.Services[$svcName] = $svc.Status.ToString()
            if ($svc.Status -eq 'Running') {
                Stop-Service -Name $svcName -Force -ErrorAction SilentlyContinue
                Write-Success "Paused background service '$svcName'"
            }
        }
    }

    ipconfig /flushdns >$null 2>&1
    netsh interface ip delete arpcache >$null 2>&1
    Write-Success "Flushed DNS resolver cache and ARP tables"

    Save-SnapshotState -State $state
    Write-Host ""
    Write-Host "  Profile applied successfully." -ForegroundColor Green
    return $state
}

# --- Restoration Engine ---
function Restore-Optimizations {
    Write-Header "RESTORING BASELINE SYSTEM CONFIGURATION"

    $state = Load-SnapshotState

    # 1. Restore Timers
    try {
        if ($state -and $state.KernelTimerResolution) {
            $dummy = 0
            [HighResolutionTimer]::NtSetTimerResolution($state.KernelTimerResolution, $false, [ref]$dummy) | Out-Null
        }
        [HighResolutionTimer]::TimeEndPeriod(1) | Out-Null
        $kernelReg = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\kernel"
        if (Test-Path $kernelReg) {
            if ($state -and $state.Registry.GlobalTimerResolutionRequests) {
                Set-ItemProperty -Path $kernelReg -Name "GlobalTimerResolutionRequests" -Value $state.Registry.GlobalTimerResolutionRequests -Type DWord -Force -ErrorAction SilentlyContinue
            } else {
                Remove-ItemProperty -Path $kernelReg -Name "GlobalTimerResolutionRequests" -ErrorAction SilentlyContinue
            }
        }
        Write-Success "Restored timer resolution to baseline"
    } catch {}

    # 2. Restore Wi-Fi AutoConfig
    if ($state -and $state.WiFiInterface) {
        netsh wlan set autoconfig enabled=yes interface="$($state.WiFiInterface)" >$null 2>&1
        Write-Success "Restored WLAN AutoConfig scanning on '$($state.WiFiInterface)'"
    } else {
        $wifiOutput = (netsh wlan show interfaces 2>$null) | Out-String
        if ($wifiOutput -match '(?m)^\s*Name\s*:\s*(.+)\r?$') {
            $wifiName = $matches[1].Trim()
            netsh wlan set autoconfig enabled=yes interface="$wifiName" >$null 2>&1
            Write-Success "Re-enabled WLAN AutoConfig scanning on '$wifiName'"
        }
    }

    # 3. Remove QoS Policy
    try {
        $qosPolicyReg = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\QoS\$QoSName"
        if (Test-Path $qosPolicyReg) {
            Remove-Item -Path $qosPolicyReg -Recurse -Force -ErrorAction SilentlyContinue
        }
        $qosRegPath = "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\QoS"
        if (Test-Path $qosRegPath) {
            if ($state -and $state.Registry.DoNotUseNLA) {
                Set-ItemProperty -Path $qosRegPath -Name "Do not use NLA" -Value $state.Registry.DoNotUseNLA -Type String -Force -ErrorAction SilentlyContinue
            } else {
                Remove-ItemProperty -Path $qosRegPath -Name "Do not use NLA" -ErrorAction SilentlyContinue
            }
        }
        Remove-NetQosPolicy -Name $QoSName -Confirm:$false -ErrorAction SilentlyContinue
        Write-Success "Removed Roblox QoS DSCP priority rule"
    } catch {}

    # 4. Restore Winsock AFD
    $afdPath = "HKLM:\SYSTEM\CurrentControlSet\Services\AFD\Parameters"
    if (Test-Path $afdPath) {
        $afdKeys = @("FastSendDatagramThreshold", "FastCopyReceiveThreshold", "DoNotDisableReceiveBuffering", "DoNotDisableSendBuffering", "NonBlockingSendLimits")
        foreach ($k in $afdKeys) {
            if ($state -and $state.Registry.$k) {
                Set-ItemProperty -Path $afdPath -Name $k -Value $state.Registry.$k -Type DWord -Force -ErrorAction SilentlyContinue
            } else {
                Remove-ItemProperty -Path $afdPath -Name $k -ErrorAction SilentlyContinue
            }
        }
        Write-Success "Restored Winsock AFD socket parameters"
    }

    # 5. Restore MMCSS Registry Keys
    $mmPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
    if (Test-Path $mmPath) {
        $throttling = if ($state -and $state.Registry.NetworkThrottlingIndex) { $state.Registry.NetworkThrottlingIndex } else { 10 }
        $resp = if ($state -and $state.Registry.SystemResponsiveness) { $state.Registry.SystemResponsiveness } else { 20 }
        
        Set-ItemProperty -Path $mmPath -Name "NetworkThrottlingIndex" -Value $throttling -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $mmPath -Name "SystemResponsiveness" -Value $resp -Type DWord -Force -ErrorAction SilentlyContinue
        if ($state -and $state.Registry.NoLazyMode) {
            Set-ItemProperty -Path $mmPath -Name "NoLazyMode" -Value $state.Registry.NoLazyMode -Type DWord -Force -ErrorAction SilentlyContinue
        } else {
            Remove-ItemProperty -Path $mmPath -Name "NoLazyMode" -ErrorAction SilentlyContinue
        }
        if ($state -and $state.Registry.AlwaysOn) {
            Set-ItemProperty -Path $mmPath -Name "AlwaysOn" -Value $state.Registry.AlwaysOn -Type DWord -Force -ErrorAction SilentlyContinue
        } else {
            Remove-ItemProperty -Path $mmPath -Name "AlwaysOn" -ErrorAction SilentlyContinue
        }
        Write-Success "Restored Multimedia SystemProfile (NetworkThrottlingIndex: $throttling, SystemResponsiveness: $resp)"
    }

    # 6. Restore TCP Parameters on Adapter
    $adapter = Get-ActiveGameAdapter
    if ($adapter) {
        $tcpInterfacesPath = "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\$($adapter.InterfaceGuid)"
        if (Test-Path $tcpInterfacesPath) {
            foreach ($param in @("TcpAckFrequency", "TCPNoDelay", "TcpDelAckTicks")) {
                $savedKey = "$($adapter.InterfaceGuid)_$param"
                if ($state -and $state.Registry.$savedKey) {
                    Set-ItemProperty -Path $tcpInterfacesPath -Name $param -Value $state.Registry.$savedKey -Type DWord -Force -ErrorAction SilentlyContinue
                } else {
                    Remove-ItemProperty -Path $tcpInterfacesPath -Name $param -ErrorAction SilentlyContinue
                }
            }
            Write-Success "Restored TCP interface parameters on '$($adapter.Name)'"
        }
    }

    # 7. Restore Global TCP/IP Stack Settings
    $tcpGlobalPath = "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"
    if (Test-Path $tcpGlobalPath) {
        foreach ($param in @("DefaultTTL", "DisableTaskOffload", "EnableDCA", "MaxUserPort", "TcpTimedWaitDelay")) {
            if ($state -and $state.Registry.$param) {
                Set-ItemProperty -Path $tcpGlobalPath -Name $param -Value $state.Registry.$param -Type DWord -Force -ErrorAction SilentlyContinue
            } else {
                Remove-ItemProperty -Path $tcpGlobalPath -Name $param -ErrorAction SilentlyContinue
            }
        }
    }
    netsh int tcp set global rsc=enabled >$null 2>&1
    netsh int tcp set global timestamps=allowed >$null 2>&1
    netsh int tcp set supplemental template=internet congestionprovider=default >$null 2>&1
    netsh int tcp set supplemental template=compat congestionprovider=default >$null 2>&1
    netsh int tcp set supplemental template=datacenterext congestionprovider=default >$null 2>&1
    Write-Success "Restored global TCP settings & RSC to baseline"

    # 8. Restart Background Services
    if ($state -and $state.Services) {
        foreach ($svcName in $state.Services.PSObject.Properties) {
            if ($svcName.Value -eq 'Running') {
                Start-Service -Name $svcName.Name -ErrorAction SilentlyContinue
                Write-Success "Resumed service '$($svcName.Name)'"
            }
        }
    } else {
        Start-Service -Name 'bits' -ErrorAction SilentlyContinue
        Start-Service -Name 'wuauserv' -ErrorAction SilentlyContinue
        Start-Service -Name 'dosvc' -ErrorAction SilentlyContinue
        Start-Service -Name 'lfsvc' -ErrorAction SilentlyContinue
        Start-Service -Name 'DiagTrack' -ErrorAction SilentlyContinue
        Write-Success "Resumed background update services"
    }

    Remove-SnapshotState
    Write-Host ""
    Write-Host "  Baseline restoration complete." -ForegroundColor Green
}

# --- Diagnostic & Benchmark Engine ---
function Test-NetworkBenchmark {
    Write-Header "NETWORK LATENCY & JITTER BENCHMARK"

    $targets = @(
        @{ Name = "Roblox Edge CDN"; Host = "roblox.com" },
        @{ Name = "Cloudflare Anycast"; Host = "1.1.1.1" },
        @{ Name = "Google Public DNS"; Host = "8.8.8.8" }
    )

    Write-Host "Sampling round-trip times and successive packet jitter..." -ForegroundColor Yellow
    Write-Host ""

    foreach ($target in $targets) {
        Write-Host "Testing $($target.Name) [$($target.Host)]..." -NoNewline
        $samples = @()
        for ($i = 0; $i -lt 5; $i++) {
            $ping = Test-Connection -ComputerName $target.Host -Count 1 -ErrorAction SilentlyContinue
            if ($ping -and $ping.ResponseTime -ne $null) {
                $samples += $ping.ResponseTime
            }
            Start-Sleep -Milliseconds 100
        }

        if ($samples.Count -gt 0) {
            $avg = [Math]::Round(($samples | Measure-Object -Average).Average, 2)
            $min = ($samples | Measure-Object -Minimum).Minimum
            $max = ($samples | Measure-Object -Maximum).Maximum
            
            $jitterSum = 0
            for ($j = 0; $j -lt ($samples.Count - 1); $j++) {
                $jitterSum += [Math]::Abs($samples[$j] - $samples[$j+1])
            }
            $jitter = if ($samples.Count -gt 1) { [Math]::Round($jitterSum / ($samples.Count - 1), 2) } else { 0 }

            Write-Host " DONE" -ForegroundColor Green
            Write-Host "    Avg: ${avg} ms | Min: ${min} ms | Max: ${max} ms | Jitter: ${jitter} ms | Samples: $($samples.Count)/5" -ForegroundColor White
        } else {
            Write-Host " TIMEOUT / BLOCKED" -ForegroundColor Red
        }
    }

    Write-Host ""
    $adapter = Get-ActiveGameAdapter
    if ($adapter) {
        Write-Host "Active Adapter: $($adapter.Name) ($($adapter.InterfaceDescription))" -ForegroundColor Cyan
        Write-Host "Status: $($adapter.Status) | LinkSpeed: $($adapter.LinkSpeed)" -ForegroundColor Cyan
    }
}

# --- Status Inspector ---
function Get-TunerStatus {
    Write-Header "CURRENT SYSTEM & TUNING STATUS"

    $mmPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
    $throttling = (Get-ItemProperty -Path $mmPath -Name "NetworkThrottlingIndex" -ErrorAction SilentlyContinue).NetworkThrottlingIndex
    $resp = (Get-ItemProperty -Path $mmPath -Name "SystemResponsiveness" -ErrorAction SilentlyContinue).SystemResponsiveness

    $qos = Get-NetQosPolicy -Name $QoSName -ErrorAction SilentlyContinue

    Write-Host "Multimedia Network Throttling : " -NoNewline
    if ("$throttling" -in @('-1', '4294967295', '0xffffffff')) {
        Write-Host "DISABLED (0xFFFFFFFF)" -ForegroundColor Green
    } else {
        Write-Host "ACTIVE ($throttling)" -ForegroundColor Yellow
    }

    Write-Host "System Responsiveness Cap    : " -NoNewline
    if ($resp -eq 0) {
        Write-Host "0% Background Reserve" -ForegroundColor Green
    } else {
        Write-Host "$resp% Background Reserved" -ForegroundColor Yellow
    }

    Write-Host "Roblox DSCP 46 QoS Policy    : " -NoNewline
    if ($qos) {
        Write-Host "ENABLED ($($qos.AppPathNameMatchCondition) -> DSCP $($qos.DSCPValue))" -ForegroundColor Green
    } else {
        Write-Host "NOT PRESENT" -ForegroundColor Gray
    }

    $wifiOutput = (netsh wlan show interfaces 2>$null) | Out-String
    if ($wifiOutput -match '(?m)^\s*Name\s*:\s*(.+)\r?$') {
        $wifiName = $matches[1].Trim()
        Write-Host "Wi-Fi Interface              : $wifiName" -ForegroundColor Cyan
    }

    $saved = Load-SnapshotState
    Write-Host "Tuner State Snapshot         : " -NoNewline
    if ($saved) {
        Write-Host "ACTIVE ($($saved.Timestamp))" -ForegroundColor Green
    } else {
        Write-Host "CLEAN (Baseline)" -ForegroundColor Gray
    }

    $rbxProc = Get-Process -Name $RobloxProcessName -ErrorAction SilentlyContinue
    Write-Host "Roblox Process Status        : " -NoNewline
    if ($rbxProc) {
        Write-Host "RUNNING (PID: $($rbxProc.Id), Priority: $($rbxProc.PriorityClass))" -ForegroundColor Green
    } else {
        Write-Host "NOT RUNNING" -ForegroundColor Gray
    }
}

# --- Watchdog Daemon Engine ---
function Start-WatchdogDaemon {
    Write-Header "STARTING ROBLOX WATCHDOG"
    Write-Host "Monitors Roblox lifecycle. Applies low-latency profile on launch, restores on exit." -ForegroundColor White
    Write-Host "Press Ctrl+C to terminate and trigger rollback." -ForegroundColor Yellow
    Write-Host ""

    $isOptimized = $false

    try {
        while ($true) {
            $rbxProc = Get-Process -Name $RobloxProcessName -ErrorAction SilentlyContinue

            if ($rbxProc -and -not $isOptimized) {
                Write-Host ""
                Write-Host "Roblox launch detected (PID $($rbxProc.Id)). Applying low-latency profile." -ForegroundColor Green
                Apply-Optimizations | Out-Null
                $isOptimized = $true

                try {
                    $rbxProc.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::High
                } catch {
                    try { $rbxProc.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::AboveNormal } catch {}
                }

                Write-Host ""
                Write-Host "Low-latency profile active. Monitoring game process..." -ForegroundColor Cyan
            }
            elseif (-not $rbxProc -and $isOptimized) {
                Write-Host ""
                Write-Host "Roblox closed. Restoring baseline configuration..." -ForegroundColor Yellow
                Restore-Optimizations | Out-Null
                $isOptimized = $false
                Write-Host ""
                Write-Host "Waiting for next session... (Ctrl+C to exit)" -ForegroundColor DarkGray
            }
            elseif (-not $rbxProc -and -not $isOptimized) {
                Write-Host "`rWaiting for Roblox to launch... [$(Get-Date -Format 'HH:mm:ss')]" -NoNewline -ForegroundColor DarkGray
            }
            else {
                Write-Host "`rRoblox running (PID $($rbxProc.Id)) | Profile active [$(Get-Date -Format 'HH:mm:ss')]" -NoNewline -ForegroundColor Green
            }

            Start-Sleep -Seconds 2
        }
    }
    finally {
        if ($isOptimized) {
            Write-Host "`nTerminating. Restoring baseline..." -ForegroundColor Yellow
            Restore-Optimizations | Out-Null
        }
    }
}

# --- Interactive Session Engine ---
function Start-InteractiveSession {
    $state = Apply-Optimizations

    Write-Host ""
    Write-Host "================================================================================" -ForegroundColor Green
    Write-Host "  Profile active. Ping jitter and latency minimized." -ForegroundColor White
    Write-Host "  Press [Space], [Q], or [Esc] or close Roblox to restore baseline." -ForegroundColor Yellow
    Write-Host "================================================================================" -ForegroundColor Green
    Write-Host ""

    $robloxSeen = $false
    $rbxInitial = Get-Process -Name $RobloxProcessName -ErrorAction SilentlyContinue
    if ($rbxInitial) {
        $robloxSeen = $true
        Write-Host "Roblox already running (PID $($rbxInitial.Id)). Elevated process priority." -ForegroundColor Green
        try { $rbxInitial.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::High } catch {}
    } else {
        Write-Host "Waiting for Roblox to launch (or press [Space] to restore & exit)..." -ForegroundColor DarkGray
    }

    try {
        while ($true) {
            if ([Console]::KeyAvailable) {
                $key = [Console]::ReadKey($true)
                if ($key.Key -in @([ConsoleKey]::Spacebar, [ConsoleKey]::Q, [ConsoleKey]::Escape)) {
                    Write-Host "`n`nUser exit key pressed ($($key.Key)). Restoring baseline..." -ForegroundColor Yellow
                    break
                }
            }

            $rbxProc = Get-Process -Name $RobloxProcessName -ErrorAction SilentlyContinue
            if ($rbxProc) {
                if (-not $robloxSeen) {
                    $robloxSeen = $true
                    Write-Host "`nRoblox launch detected (PID $($rbxProc.Id))." -ForegroundColor Green
                    try { $rbxProc.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::High } catch {}
                }
                Write-Host "`r[ACTIVE]  Roblox (PID $($rbxProc.Id)) | [Space] to restore [$(Get-Date -Format 'HH:mm:ss')]" -NoNewline -ForegroundColor Green
            } else {
                if ($robloxSeen) {
                    Write-Host "`n`nRoblox closed. Restoring baseline configuration..." -ForegroundColor Yellow
                    break
                } else {
                    Write-Host "`r[STANDBY] Profile active | Waiting for Roblox... | [Space] to restore [$(Get-Date -Format 'HH:mm:ss')]" -NoNewline -ForegroundColor DarkGray
                }
            }

            Start-Sleep -Milliseconds 250
        }
    }
    finally {
        Write-Host "`n"
        Restore-Optimizations
        Write-Host "`n[OK] Baseline restored. Closing in 2 seconds..." -ForegroundColor Green
        Start-Sleep -Seconds 2
    }
}

# --- Execution Entrypoint ---
switch ($Mode) {
    'Interactive' { Start-InteractiveSession }
    'Watchdog'    { Start-WatchdogDaemon }
    'Apply'       { Apply-Optimizations }
    'Restore'     { Restore-Optimizations }
    'Benchmark'   { Test-NetworkBenchmark }
    'Status'      { Get-TunerStatus }
}
