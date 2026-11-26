"""Cuts the hosted runner off from the asset-bundle CDN while the player runs, then restores it (dcl-one#2144 proof).

Thumbnails of wearables come from asset bundles on ab-cdn.decentraland.org. Blocking that host makes every thumbnail
download fail the way an aborted or refused request does, without touching the player build. Four layers make the
block hold for a player that already has open connections and a resolver cache:
  * the hosts file points the host at loopback, where nothing listens, so new lookups get a refused connection;
  * an outbound firewall rule blocks the addresses the host resolved to, for anything that cached them;
  * the player's open TCP connections to those addresses are reset (SetTcpEntry, DELETE_TCB);
  * curl in the runner checks the block before the Passport opens and the restore after.
The CDN's addresses are a shared edge in front of other decentraland.org hosts as well. Under --gateway the player
reaches its services through dcl.dev, which resolves elsewhere, and the block lasts about two minutes.
Needs an elevated runner, which GitHub's hosted Windows runners are."""
import ctypes
from ctypes import wintypes
import ipaddress
from pathlib import Path
import re
import socket
import struct
import subprocess

HOST = "ab-cdn.decentraland.org"
HOSTS_FILE = Path(r"C:\Windows\System32\drivers\etc\hosts")
MARK = "# dcl-one-2144-proof"
RULE = "dcl-one-2144-proof-block-ab-cdn"
DELETE_TCB = 12


class MIB_TCPROW(ctypes.Structure):
    _fields_ = [("dwState", wintypes.DWORD), ("dwLocalAddr", wintypes.DWORD), ("dwLocalPort", wintypes.DWORD),
                ("dwRemoteAddr", wintypes.DWORD), ("dwRemotePort", wintypes.DWORD)]


def _powershell(command):
    result = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", command],
                            capture_output=True, text=True, timeout=90)
    return result.returncode, (result.stdout + result.stderr).strip()


def _flush_dns():
    subprocess.run(["ipconfig", "/flushdns"], capture_output=True, timeout=30)


def _resolved_addresses():
    addresses = set()
    for _ in range(8):
        try:
            addresses.update(info[4][0] for info in socket.getaddrinfo(HOST, 443, socket.AF_INET, socket.SOCK_STREAM))
        except OSError:
            pass
    # What the Windows resolver handed out to the player, which may be a different rotation of the same edge.
    _, cached = _powershell(f"Get-DnsClientCache -ErrorAction SilentlyContinue | Where-Object {{ $_.Entry -eq '{HOST}' }} | "
                            "ForEach-Object { $_.Data }")
    addresses.update(line.strip() for line in cached.splitlines() if re.fullmatch(r"\d+\.\d+\.\d+\.\d+", line.strip()))
    return sorted(a for a in addresses if not ipaddress.ip_address(a).is_loopback)


def _player_connections(pid, addresses):
    import psutil
    process = psutil.Process(pid)
    connections = process.net_connections(kind="tcp4") if hasattr(process, "net_connections") else process.connections(kind="tcp4")
    return [c for c in connections if c.raddr and c.raddr.ip in addresses]


def reset_player_connections(pid, addresses):
    reset = 0
    for c in _player_connections(pid, addresses):
        row = MIB_TCPROW(DELETE_TCB,
                         struct.unpack("<I", socket.inet_aton(c.laddr.ip))[0], socket.htons(c.laddr.port),
                         struct.unpack("<I", socket.inet_aton(c.raddr.ip))[0], socket.htons(c.raddr.port))
        if ctypes.windll.iphlpapi.SetTcpEntry(ctypes.byref(row)) == 0:
            reset += 1
    return reset


def probe(url):
    """HTTP status the runner gets for url right now; 0 when the connection fails."""
    result = subprocess.run(["curl.exe", "-s", "-o", "NUL", "-w", "%{http_code}", "--max-time", "10", url],
                            capture_output=True, text=True, timeout=30)
    try:
        return int(result.stdout.strip() or 0)
    except ValueError:
        return 0


def block(pid):
    addresses = _resolved_addresses()
    text = HOSTS_FILE.read_text(encoding="ascii", errors="replace")
    HOSTS_FILE.write_text(text.rstrip("\r\n") + f"\r\n127.0.0.1 {HOST} {MARK}\r\n::1 {HOST} {MARK}\r\n", encoding="ascii")
    _flush_dns()
    code, firewall = _powershell("Get-NetFirewallProfile | ForEach-Object { \"$($_.Name)=$($_.Enabled)\" }")
    profiles_before = firewall.splitlines() if code == 0 else []
    code, output = _powershell("Set-NetFirewallProfile -All -Enabled True; "
                               f"New-NetFirewallRule -DisplayName '{RULE}' -Direction Outbound -Action Block "
                               f"-RemoteAddress {','.join(addresses)} | Out-Null")
    return {"addresses": len(addresses), "firewall_profiles_before": profiles_before, "firewall_rule_ok": code == 0,
            "firewall_error": output[:200] if code else "", "connections_reset": reset_player_connections(pid, addresses),
            "addresses_list": addresses}


def unblock(profiles_before):
    text = HOSTS_FILE.read_text(encoding="ascii", errors="replace")
    HOSTS_FILE.write_text("".join(line for line in text.splitlines(True) if MARK not in line), encoding="ascii")
    _flush_dns()
    restore = "; ".join(f"Set-NetFirewallProfile -Name {name} -Enabled {enabled}"
                        for name, enabled in (p.split("=", 1) for p in profiles_before if "=" in p))
    code, output = _powershell(f"Remove-NetFirewallRule -DisplayName '{RULE}' -ErrorAction SilentlyContinue; {restore}")
    return {"firewall_restored": code == 0, "error": output[:200] if code else ""}
