#!/usr/bin/env python3
"""
Socket Monitor CLI Utility

Monitors open sockets for a selected process to investigate HTTP/2 multiplexing.
Generates reports with raw JSON data and visualizations.
"""

import argparse
import json
import os
import socket
import statistics
import sys
import threading
import time
from collections import defaultdict
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass, field, asdict
from datetime import datetime
from typing import Optional

try:
    import psutil
except ImportError:
    print("Error: psutil is required. Install with: pip install psutil")
    sys.exit(1)

try:
    import matplotlib.pyplot as plt
    import matplotlib.dates as mdates
    MATPLOTLIB_AVAILABLE = True
except ImportError:
    MATPLOTLIB_AVAILABLE = False
    print("Warning: matplotlib not available. Graphs will not be generated.")
    print("Install with: pip install matplotlib")


class DNSResolver:
    """Thread-safe DNS reverse lookup with caching."""

    def __init__(self, timeout: float = 2.0):
        self._cache: dict[str, str] = {}
        self._lock = threading.Lock()
        self._timeout = timeout

    def resolve(self, ip: str) -> str:
        """Resolve IP to hostname. Returns IP if resolution fails."""
        if not ip:
            return ""

        with self._lock:
            if ip in self._cache:
                return self._cache[ip]

        # Perform lookup outside lock
        hostname = self._do_lookup(ip)

        with self._lock:
            self._cache[ip] = hostname

        return hostname

    def _do_lookup(self, ip: str) -> str:
        """Perform the actual DNS lookup."""
        try:
            # Set socket timeout for this lookup
            old_timeout = socket.getdefaulttimeout()
            socket.setdefaulttimeout(self._timeout)
            try:
                hostname, _, _ = socket.gethostbyaddr(ip)
                return hostname
            finally:
                socket.setdefaulttimeout(old_timeout)
        except (socket.herror, socket.gaierror, socket.timeout, OSError):
            return ip  # Return IP if lookup fails

    def resolve_batch(self, ips: list[str], max_workers: int = 10) -> dict[str, str]:
        """Resolve multiple IPs in parallel."""
        results = {}
        ips_to_resolve = []

        # Check cache first
        with self._lock:
            for ip in ips:
                if ip in self._cache:
                    results[ip] = self._cache[ip]
                elif ip:
                    ips_to_resolve.append(ip)

        # Resolve uncached IPs in parallel
        if ips_to_resolve:
            with ThreadPoolExecutor(max_workers=max_workers) as executor:
                future_to_ip = {executor.submit(self._do_lookup, ip): ip for ip in ips_to_resolve}
                for future in as_completed(future_to_ip):
                    ip = future_to_ip[future]
                    try:
                        hostname = future.result()
                    except Exception:
                        hostname = ip
                    results[ip] = hostname

            # Update cache
            with self._lock:
                for ip, hostname in results.items():
                    if ip not in self._cache:
                        self._cache[ip] = hostname

        return results

    def get_cache(self) -> dict[str, str]:
        """Get a copy of the DNS cache."""
        with self._lock:
            return self._cache.copy()


# Global DNS resolver instance
dns_resolver = DNSResolver()


@dataclass
class SocketInfo:
    """Information about a single socket connection."""
    family: str  # AF_INET, AF_INET6
    type: str    # SOCK_STREAM (TCP), SOCK_DGRAM (UDP)
    local_address: str
    local_port: int
    remote_address: str
    remote_port: int
    status: str  # ESTABLISHED, LISTEN, TIME_WAIT, etc.
    remote_hostname: str = ""  # DNS resolved hostname

    def to_dict(self) -> dict:
        return asdict(self)


@dataclass
class SocketSnapshot:
    """A snapshot of all sockets at a point in time."""
    timestamp: str
    timestamp_epoch: float
    total_count: int
    by_type: dict
    by_status: dict
    by_remote: dict
    sockets: list[SocketInfo] = field(default_factory=list)

    def to_dict(self) -> dict:
        return {
            "timestamp": self.timestamp,
            "timestamp_epoch": self.timestamp_epoch,
            "total_count": self.total_count,
            "by_type": self.by_type,
            "by_status": self.by_status,
            "by_remote": self.by_remote,
            "sockets": [s.to_dict() for s in self.sockets]
        }


@dataclass
class MonitoringReport:
    """Final report after monitoring session."""
    process_name: str
    process_id: int
    start_time: str
    end_time: str
    duration_seconds: float
    interval_seconds: float
    total_snapshots: int
    max_sockets: int
    max_sockets_time: str
    max_sockets_details: list[dict]
    dns_cache: dict = field(default_factory=dict)  # IP -> hostname mapping
    snapshots: list[SocketSnapshot] = field(default_factory=list)

    def to_dict(self) -> dict:
        return {
            "process_name": self.process_name,
            "process_id": self.process_id,
            "start_time": self.start_time,
            "end_time": self.end_time,
            "duration_seconds": self.duration_seconds,
            "interval_seconds": self.interval_seconds,
            "total_snapshots": self.total_snapshots,
            "max_sockets": self.max_sockets,
            "max_sockets_time": self.max_sockets_time,
            "max_sockets_details": self.max_sockets_details,
            "dns_cache": self.dns_cache,
            "snapshots": [s.to_dict() for s in self.snapshots]
        }


def get_socket_family_name(family: int) -> str:
    """Convert socket family constant to readable name."""
    families = {
        2: "AF_INET",    # IPv4
        23: "AF_INET6",  # IPv6 (Windows)
        10: "AF_INET6",  # IPv6 (Linux)
    }
    return families.get(family, f"UNKNOWN({family})")


def get_socket_type_name(sock_type: int) -> str:
    """Convert socket type constant to readable name."""
    types = {
        1: "TCP",   # SOCK_STREAM
        2: "UDP",   # SOCK_DGRAM
    }
    return types.get(sock_type, f"UNKNOWN({sock_type})")


def list_processes() -> list[tuple[int, str]]:
    """List all running processes."""
    processes = []
    for proc in psutil.process_iter(['pid', 'name']):
        try:
            processes.append((proc.info['pid'], proc.info['name']))
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            continue
    return sorted(processes, key=lambda x: x[1].lower())


def select_process_interactive() -> Optional[tuple[int, str]]:
    """Interactive process selection."""
    processes = list_processes()

    print("\n" + "=" * 60)
    print("RUNNING PROCESSES")
    print("=" * 60)

    # Group by name for easier viewing
    by_name = defaultdict(list)
    for pid, name in processes:
        by_name[name].append(pid)

    indexed = []
    for name in sorted(by_name.keys(), key=str.lower):
        pids = by_name[name]
        indexed.append((name, pids))

    for idx, (name, pids) in enumerate(indexed, 1):
        pid_str = ", ".join(str(p) for p in pids[:3])
        if len(pids) > 3:
            pid_str += f", ... ({len(pids)} total)"
        print(f"  {idx:4d}. {name:<40} [PIDs: {pid_str}]")

    print("\n" + "-" * 60)
    print("Enter process number, name filter, or PID directly.")
    print("Type 'q' to quit.")
    print("-" * 60)

    while True:
        try:
            choice = input("\nSelect process: ").strip()

            if choice.lower() == 'q':
                return None

            # Try as index
            if choice.isdigit():
                num = int(choice)
                # Check if it's a valid index
                if 1 <= num <= len(indexed):
                    name, pids = indexed[num - 1]
                    if len(pids) == 1:
                        return (pids[0], name)
                    else:
                        print(f"\nMultiple PIDs for {name}:")
                        for i, pid in enumerate(pids, 1):
                            print(f"  {i}. PID {pid}")
                        sub = input("Select PID number: ").strip()
                        if sub.isdigit() and 1 <= int(sub) <= len(pids):
                            return (pids[int(sub) - 1], name)
                        continue
                # Check if it's a direct PID
                elif num in [p[0] for p in processes]:
                    for pid, name in processes:
                        if pid == num:
                            return (pid, name)

            # Try as name filter
            matches = [(n, p) for n, p in indexed if choice.lower() in n.lower()]
            if len(matches) == 1:
                name, pids = matches[0]
                if len(pids) == 1:
                    return (pids[0], name)
                else:
                    print(f"\nMultiple PIDs for {name}:")
                    for i, pid in enumerate(pids, 1):
                        print(f"  {i}. PID {pid}")
                    sub = input("Select PID number: ").strip()
                    if sub.isdigit() and 1 <= int(sub) <= len(pids):
                        return (pids[int(sub) - 1], name)
            elif len(matches) > 1:
                print(f"\nMultiple matches for '{choice}':")
                for i, (name, pids) in enumerate(matches, 1):
                    print(f"  {i}. {name}")
                continue
            else:
                print(f"No process found matching '{choice}'")

        except KeyboardInterrupt:
            return None


# Socket statuses considered "live" (active connections)
LIVE_SOCKET_STATUSES = {
    "ESTABLISHED",  # Active connection
    "SYN_SENT",     # Connection initiation (client)
    "SYN_RECV",     # Connection being accepted (server)
    "NONE",         # UDP sockets (stateless)
}

# Socket statuses considered "closing" or "closed" (excluded from monitoring)
CLOSED_SOCKET_STATUSES = {
    "TIME_WAIT",    # Waiting after close
    "CLOSE_WAIT",   # Remote closed, waiting for local close
    "LAST_ACK",     # Waiting for final ACK
    "FIN_WAIT1",    # Initiating close
    "FIN_WAIT2",    # Waiting for FIN from remote
    "CLOSING",      # Both sides closing simultaneously
    "CLOSED",       # Fully closed
    "LISTEN",       # Listening sockets have no remote address
}


def _get_local_ips() -> set[str]:
    """Get all IP addresses of local network interfaces."""
    local_ips = set()
    try:
        for iface, addrs in psutil.net_if_addrs().items():
            for addr in addrs:
                if addr.family in (socket.AF_INET, socket.AF_INET6):
                    local_ips.add(addr.address)
    except Exception:
        pass
    return local_ips


def _is_loopback(ip: str) -> bool:
    """Check if IP is a loopback address."""
    if not ip:
        return False
    # IPv4 loopback
    if ip.startswith("127."):
        return True
    # IPv6 loopback
    if ip == "::1" or ip.lower() == "0:0:0:0:0:0:0:1":
        return True
    return False


def _is_local_or_loopback(ip: str, local_ips: set[str]) -> bool:
    """Check if IP is local (same machine) or loopback."""
    if not ip:
        return True  # Empty = no remote, treat as local
    if _is_loopback(ip):
        return True
    if ip in local_ips:
        return True
    return False


def get_process_sockets(pid: int, resolve_dns: bool = True, live_only: bool = True) -> list[SocketInfo]:
    """Get socket connections for a process.

    Args:
        pid: Process ID to monitor
        resolve_dns: Whether to resolve IPs to hostnames
        live_only: If True, only return live/active sockets (excludes TIME_WAIT, CLOSE_WAIT, etc.)

    Returns:
        List of SocketInfo for remote connections only (excludes local/loopback).
    """
    sockets = []
    try:
        proc = psutil.Process(pid)
        connections = proc.net_connections(kind='all')

        # Get local IPs to filter out local connections
        local_ips = _get_local_ips()

        # Collect all remote IPs for batch resolution
        remote_ips = set()
        conn_data = []

        for conn in connections:
            status = conn.status if conn.status else "NONE"

            # Filter out closed/closing sockets if live_only is enabled
            if live_only and status not in LIVE_SOCKET_STATUSES:
                continue

            remote_addr = conn.raddr.ip if conn.raddr else ""
            remote_port = conn.raddr.port if conn.raddr else 0

            # Skip local and loopback addresses - only interested in real remote connections
            if _is_local_or_loopback(remote_addr, local_ips):
                continue

            local_addr = conn.laddr.ip if conn.laddr else ""
            local_port = conn.laddr.port if conn.laddr else 0

            conn_data.append({
                'family': get_socket_family_name(conn.family),
                'type': get_socket_type_name(conn.type),
                'local_address': local_addr,
                'local_port': local_port,
                'remote_address': remote_addr,
                'remote_port': remote_port,
                'status': status
            })

            remote_ips.add(remote_addr)

        # Batch resolve DNS (uses cache, so fast for repeated IPs)
        hostnames = {}
        if resolve_dns and remote_ips:
            hostnames = dns_resolver.resolve_batch(list(remote_ips))

        # Create SocketInfo objects with resolved hostnames
        for data in conn_data:
            remote_addr = data['remote_address']
            hostname = hostnames.get(remote_addr, remote_addr) if remote_addr else ""

            socket_info = SocketInfo(
                family=data['family'],
                type=data['type'],
                local_address=data['local_address'],
                local_port=data['local_port'],
                remote_address=remote_addr,
                remote_port=data['remote_port'],
                status=data['status'],
                remote_hostname=hostname
            )
            sockets.append(socket_info)

    except (psutil.NoSuchProcess, psutil.AccessDenied) as e:
        print(f"Error accessing process: {e}")

    return sockets


def create_snapshot(sockets: list[SocketInfo]) -> SocketSnapshot:
    """Create a snapshot from socket list."""
    now = datetime.now()

    by_type = defaultdict(int)
    by_status = defaultdict(int)
    by_remote = defaultdict(int)

    for sock in sockets:
        by_type[sock.type] += 1
        by_status[sock.status] += 1
        if sock.remote_address:
            # Use hostname if available, otherwise IP
            host = sock.remote_hostname if sock.remote_hostname != sock.remote_address else sock.remote_address
            remote_key = f"{host}:{sock.remote_port}"
            by_remote[remote_key] += 1

    return SocketSnapshot(
        timestamp=now.isoformat(),
        timestamp_epoch=now.timestamp(),
        total_count=len(sockets),
        by_type=dict(by_type),
        by_status=dict(by_status),
        by_remote=dict(by_remote),
        sockets=sockets
    )


def generate_graph(report: MonitoringReport, output_path: str):
    """Generate a graph of socket counts over time."""
    if not MATPLOTLIB_AVAILABLE:
        print("Cannot generate graph: matplotlib not available")
        return

    if not report.snapshots:
        print("No data to graph")
        return

    # Extract data
    times = [datetime.fromisoformat(s.timestamp) for s in report.snapshots]
    counts = [s.total_count for s in report.snapshots]

    # Create figure - single plot for remote sockets
    fig, ax = plt.subplots(figsize=(12, 5))

    # Remote sockets plot
    ax.plot(times, counts, 'b-', linewidth=2, label='Remote Sockets')
    ax.fill_between(times, counts, alpha=0.3)
    ax.set_xlabel('Time')
    ax.set_ylabel('Concurrent Remote Sockets')
    ax.set_title(f'Remote Sockets: {report.process_name} (PID: {report.process_id})\n(Excludes loopback, local, LISTEN, TIME_WAIT, etc.)')
    ax.legend(loc='upper left')
    ax.grid(True, alpha=0.3)

    # Mark max point
    max_idx = counts.index(max(counts))
    ax.annotate(f'Max: {max(counts)}',
                xy=(times[max_idx], counts[max_idx]),
                xytext=(10, 10), textcoords='offset points',
                arrowprops=dict(arrowstyle='->', color='red'),
                color='red', fontweight='bold')

    # Mark median line
    median_count = statistics.median(counts)
    ax.axhline(y=median_count, color='orange', linestyle='--', linewidth=1.5, label=f'Median: {median_count:.0f}')
    ax.legend(loc='upper left')

    # Format x-axis
    ax.xaxis.set_major_formatter(mdates.DateFormatter('%H:%M:%S'))
    plt.xticks(rotation=45)

    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches='tight')
    plt.close()

    print(f"Graph saved to: {output_path}")


def _format_socket_detail(detail: dict) -> str:
    """Format a socket detail for display."""
    remote_addr = detail.get('remote_address', '')
    remote_host = detail.get('remote_hostname', '')
    remote_port = detail.get('remote_port', 0)

    # Show hostname if different from IP
    if remote_host and remote_host != remote_addr:
        remote_display = f"{remote_host} ({remote_addr}):{remote_port}"
    elif remote_addr:
        remote_display = f"{remote_addr}:{remote_port}"
    else:
        remote_display = "(none)"

    return f"  {detail['type']:4} {detail['status']:12} -> {remote_display}"


def generate_summary(report: MonitoringReport) -> str:
    """Generate the text summary report."""
    lines = []

    lines.append("=" * 70)
    lines.append("SOCKET MONITORING SUMMARY")
    lines.append("=" * 70)
    lines.append("")

    # Basic info
    lines.append("PROCESS INFORMATION")
    lines.append("-" * 70)
    lines.append(f"  Process Name: {report.process_name}")
    lines.append(f"  Process ID:   {report.process_id}")
    lines.append(f"  Start Time:   {report.start_time}")
    lines.append(f"  End Time:     {report.end_time}")
    lines.append(f"  Duration:     {report.duration_seconds:.1f} seconds")
    lines.append(f"  Interval:     {report.interval_seconds}s")
    lines.append(f"  Snapshots:    {report.total_snapshots}")
    lines.append("")

    # Statistics
    lines.append("SOCKET STATISTICS")
    lines.append("-" * 70)

    socket_counts = [s.total_count for s in report.snapshots] if report.snapshots else [0]

    max_count = max(socket_counts)
    min_count = min(socket_counts)
    avg_count = sum(socket_counts) / len(socket_counts) if socket_counts else 0
    median_count = statistics.median(socket_counts) if socket_counts else 0

    lines.append(f"  Maximum concurrent sockets: {max_count}")
    lines.append(f"  Minimum concurrent sockets: {min_count}")
    lines.append(f"  Average concurrent sockets: {avg_count:.1f}")
    lines.append(f"  Median concurrent sockets:  {median_count:.1f}")
    lines.append(f"  Max sockets occurred at:    {report.max_sockets_time}")
    lines.append("")

    # Unique remote endpoints
    all_remotes = set()
    for snapshot in report.snapshots:
        for sock in snapshot.sockets:
            if sock.remote_address:
                host = sock.remote_hostname if sock.remote_hostname != sock.remote_address else sock.remote_address
                all_remotes.add(f"{host}:{sock.remote_port}")

    lines.append(f"  Unique remote endpoints:    {len(all_remotes)}")
    lines.append("")

    # Max sockets breakdown (grouped by address)
    if report.max_sockets_details:
        lines.append("MAX CONCURRENT SOCKETS BREAKDOWN (grouped by address)")
        lines.append("-" * 70)
        lines.append(f"  Total: {len(report.max_sockets_details)} sockets at peak")
        lines.append("")

        # Group by remote address
        by_address = defaultdict(list)
        for detail in report.max_sockets_details:
            remote_addr = detail.get('remote_address', '')
            remote_host = detail.get('remote_hostname', '')
            remote_port = detail.get('remote_port', 0)

            # Use hostname if available, otherwise IP
            if remote_host and remote_host != remote_addr:
                key = f"{remote_host} ({remote_addr})"
            elif remote_addr:
                key = remote_addr
            else:
                key = "(unknown)"

            by_address[key].append({
                'type': detail.get('type', '?'),
                'status': detail.get('status', '?'),
                'port': remote_port
            })

        # Sort by connection count (descending), then by address
        sorted_addresses = sorted(by_address.items(), key=lambda x: (-len(x[1]), x[0]))

        for address, sockets in sorted_addresses:
            lines.append(f"  {address}")
            lines.append(f"    Connections: {len(sockets)}")

            # Group by port within this address
            by_port = defaultdict(list)
            for sock in sockets:
                by_port[sock['port']].append(sock)

            for port, port_sockets in sorted(by_port.items()):
                status_counts = defaultdict(int)
                for s in port_sockets:
                    status_counts[f"{s['type']}/{s['status']}"] += 1

                status_str = ", ".join(f"{k}: {v}" for k, v in sorted(status_counts.items()))
                lines.append(f"      :{port} [{status_str}]")

            lines.append("")

    # All unique remote endpoints
    if all_remotes:
        lines.append("ALL UNIQUE REMOTE ENDPOINTS")
        lines.append("-" * 70)
        for endpoint in sorted(all_remotes):
            lines.append(f"  {endpoint}")
        lines.append("")

    # DNS resolution
    if report.dns_cache:
        resolved_count = sum(1 for ip, host in report.dns_cache.items() if ip != host)
        lines.append("DNS RESOLUTION")
        lines.append("-" * 70)
        lines.append(f"  Resolved: {resolved_count}/{len(report.dns_cache)} IPs")
        lines.append("")
        for ip, hostname in sorted(report.dns_cache.items()):
            if ip != hostname:
                lines.append(f"  {ip:40} -> {hostname}")
            else:
                lines.append(f"  {ip:40} (unresolved)")
        lines.append("")

    lines.append("=" * 70)
    lines.append("END OF REPORT")
    lines.append("=" * 70)

    return "\n".join(lines)


def save_report(report: MonitoringReport, output_dir: str):
    """Save the monitoring report."""
    os.makedirs(output_dir, exist_ok=True)

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    base_name = f"socket_report_{report.process_name}_{timestamp}"

    # Save JSON report
    json_path = os.path.join(output_dir, f"{base_name}.json")
    with open(json_path, 'w', encoding='utf-8') as f:
        json.dump(report.to_dict(), f, indent=2)
    print(f"JSON report saved to: {json_path}")

    # Generate and save graph
    if MATPLOTLIB_AVAILABLE:
        graph_path = os.path.join(output_dir, f"{base_name}.png")
        generate_graph(report, graph_path)

    # Generate summary
    summary = generate_summary(report)

    # Save summary to file
    summary_path = os.path.join(output_dir, f"{base_name}_summary.txt")
    with open(summary_path, 'w', encoding='utf-8') as f:
        f.write(summary)
    print(f"Summary saved to: {summary_path}")

    # Print summary to console
    print("\n" + summary)


class SocketMonitor:
    """Main socket monitoring class."""

    def __init__(self, pid: int, process_name: str, interval: float = 0.5, resolve_dns: bool = True):
        self.pid = pid
        self.process_name = process_name
        self.interval = interval
        self.resolve_dns = resolve_dns
        self.snapshots: list[SocketSnapshot] = []
        self.start_time: Optional[datetime] = None
        self._monitor_thread: Optional[threading.Thread] = None
        self._lock = threading.Lock()
        self._stop_event = threading.Event()

    @property
    def running(self) -> bool:
        return not self._stop_event.is_set()

    def start(self):
        """Start monitoring."""
        self._stop_event.clear()
        self.start_time = datetime.now()
        # Non-daemon thread to ensure clean shutdown
        self._monitor_thread = threading.Thread(target=self._monitor_loop, daemon=False)
        self._monitor_thread.start()
        print(f"\nMonitoring started for {self.process_name} (PID: {self.pid})")
        print(f"Interval: {self.interval}s")
        print("Tracking: LIVE REMOTE sockets only (ESTABLISHED, SYN_SENT, SYN_RECV, UDP)")
        print("Excluded: Loopback, local IPs, LISTEN, TIME_WAIT, CLOSE_WAIT, FIN_WAIT, CLOSED")
        print("Type '/stop' to stop and generate report.\n")

    def stop(self) -> MonitoringReport:
        """Stop monitoring and return report."""
        self._stop_event.set()
        if self._monitor_thread and self._monitor_thread.is_alive():
            self._monitor_thread.join(timeout=3.0)

        end_time = datetime.now()
        duration = (end_time - self.start_time).total_seconds() if self.start_time else 0

        # Find max
        max_snapshot = max(self.snapshots, key=lambda s: s.total_count) if self.snapshots else None

        return MonitoringReport(
            process_name=self.process_name,
            process_id=self.pid,
            start_time=self.start_time.isoformat() if self.start_time else "",
            end_time=end_time.isoformat(),
            duration_seconds=duration,
            interval_seconds=self.interval,
            total_snapshots=len(self.snapshots),
            max_sockets=max_snapshot.total_count if max_snapshot else 0,
            max_sockets_time=max_snapshot.timestamp if max_snapshot else "",
            max_sockets_details=[s.to_dict() for s in max_snapshot.sockets] if max_snapshot else [],
            dns_cache=dns_resolver.get_cache(),
            snapshots=self.snapshots.copy()
        )

    def _monitor_loop(self):
        """Main monitoring loop."""
        next_tick = time.perf_counter()

        while not self._stop_event.is_set():
            iteration_start = time.perf_counter()

            try:
                # Check if process still exists
                if not psutil.pid_exists(self.pid):
                    self._safe_print(f"\nProcess {self.pid} no longer exists!")
                    self._stop_event.set()
                    break

                sockets = get_process_sockets(self.pid, resolve_dns=self.resolve_dns)
                snapshot = create_snapshot(sockets)

                with self._lock:
                    self.snapshots.append(snapshot)

                # Display current status (only if not stopping)
                if not self._stop_event.is_set():
                    status_parts = [f"[{datetime.now().strftime('%H:%M:%S')}]"]
                    status_parts.append(f"Remote: {snapshot.total_count}")

                    if snapshot.by_type:
                        type_str = ", ".join(f"{k}:{v}" for k, v in snapshot.by_type.items())
                        status_parts.append(f"({type_str})")

                    # Show status breakdown for live sockets
                    if snapshot.by_status:
                        status_strs = [f"{k}:{v}" for k, v in snapshot.by_status.items()]
                        status_parts.append(f"[{', '.join(status_strs)}]")

                    self._safe_print(" ".join(status_parts), end="\r")

            except Exception as e:
                if not self._stop_event.is_set():
                    self._safe_print(f"\nError during monitoring: {e}")

            # Calculate time to wait to maintain accurate interval
            # Schedule next tick based on interval, not on when work finished
            next_tick += self.interval
            sleep_time = next_tick - time.perf_counter()

            # If we're behind schedule (work took longer than interval), catch up
            if sleep_time < 0:
                # Skip missed ticks and schedule next one from now
                missed = int(-sleep_time / self.interval) + 1
                next_tick += missed * self.interval
                sleep_time = next_tick - time.perf_counter()

            if sleep_time > 0:
                self._stop_event.wait(timeout=sleep_time)

    def _safe_print(self, *args, **kwargs):
        """Print that won't fail during interpreter shutdown."""
        try:
            print(*args, **kwargs, flush=True)
        except (OSError, ValueError):
            # Ignore errors during shutdown
            pass


def main():
    parser = argparse.ArgumentParser(
        description="Socket Monitor CLI - Monitor open sockets for HTTP/2 multiplexing investigation"
    )
    parser.add_argument(
        "-p", "--pid",
        type=int,
        help="Process ID to monitor (skips interactive selection)"
    )
    parser.add_argument(
        "-i", "--interval",
        type=float,
        default=0.5,
        help="Monitoring interval in seconds (default: 0.5)"
    )
    parser.add_argument(
        "-o", "--output",
        type=str,
        default="socket_reports",
        help="Output directory for reports (default: socket_reports)"
    )
    parser.add_argument(
        "-d", "--duration",
        type=float,
        default=None,
        help="Duration in seconds to run the monitor (auto-stops after this time)"
    )
    parser.add_argument(
        "--no-dns",
        action="store_true",
        help="Disable DNS reverse lookup for remote IPs"
    )
    parser.add_argument(
        "--dns-timeout",
        type=float,
        default=2.0,
        help="DNS lookup timeout in seconds (default: 2.0)"
    )

    args = parser.parse_args()

    # Configure DNS resolver
    if not args.no_dns:
        dns_resolver._timeout = args.dns_timeout

    print("\n" + "=" * 60)
    print("  SOCKET MONITOR - HTTP/2 Multiplexing Investigation Tool")
    print("=" * 60)

    # Select process
    if args.pid:
        try:
            proc = psutil.Process(args.pid)
            selected = (args.pid, proc.name())
        except psutil.NoSuchProcess:
            print(f"Error: Process with PID {args.pid} not found")
            sys.exit(1)
    else:
        selected = select_process_interactive()

    if not selected:
        print("No process selected. Exiting.")
        sys.exit(0)

    pid, process_name = selected
    print(f"\nSelected: {process_name} (PID: {pid})")

    # Start monitoring
    resolve_dns = not args.no_dns
    monitor = SocketMonitor(pid, process_name, args.interval, resolve_dns=resolve_dns)
    monitor.start()

    if resolve_dns:
        print("DNS resolution: enabled")
    else:
        print("DNS resolution: disabled")

    if args.duration:
        print(f"Auto-stop after: {args.duration}s")

    # Wait for stop condition
    try:
        if args.duration:
            # Auto-stop mode: wait for specified duration
            start_time = time.perf_counter()
            while monitor.running:
                elapsed = time.perf_counter() - start_time
                remaining = args.duration - elapsed
                if remaining <= 0:
                    print(f"\n\nDuration of {args.duration}s reached.")
                    break
                # Check every 0.1s or remaining time, whichever is smaller
                time.sleep(min(0.1, remaining))
        else:
            # Manual mode: wait for /stop command
            while monitor.running:
                try:
                    cmd = input()
                    if cmd.strip().lower() == "/stop":
                        break
                except EOFError:
                    break
    except KeyboardInterrupt:
        pass  # Silently handle Ctrl+C

    # Stop the monitor thread first (ensures clean shutdown)
    print("\n\nStopping monitor...")
    report = monitor.stop()

    # Generate report after thread is stopped
    print("Generating report...")
    save_report(report, args.output)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        # Handle Ctrl+C at top level
        sys.exit(0)
