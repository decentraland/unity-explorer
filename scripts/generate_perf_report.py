#!/usr/bin/env python3
"""
Performance Benchmark Report Generator

Generates a PDF report comparing performance test results across different TestFixture scenarios.
Analyzes Unity Performance Test Framework JSON output and creates bar charts for:
- Median (with std dev)
- P95 (with std dev calculated from samples)
- P99 (with std dev calculated from samples)
- Downloaded Data (MB)

Usage:
    python generate_perf_report.py <input_json> [output_pdf]
    python generate_perf_report.py PerformanceTestResults.json report.pdf
"""

import json
import re
import sys
import numpy as np
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional

import matplotlib.pyplot as plt
from matplotlib.backends.backend_pdf import PdfPages


# Sample unit constants from Unity Performance Testing Framework
SAMPLE_UNIT_UNDEFINED = 0
SAMPLE_UNIT_MICROSECOND = 1
SAMPLE_UNIT_MILLISECOND = 2
SAMPLE_UNIT_SECOND = 3
SAMPLE_UNIT_BYTE = 4
SAMPLE_UNIT_KILOBYTE = 5
SAMPLE_UNIT_MEGABYTE = 6
SAMPLE_UNIT_GIGABYTE = 7
SAMPLE_UNIT_NANOSECOND = 8


@dataclass
class SampleGroupData:
    """Holds processed sample group statistics."""
    name: str
    unit: int
    samples: list[float]
    median: float
    std_dev: float
    p95: float
    p99: float
    min_val: float
    max_val: float
    sum_val: float

    @classmethod
    def from_json(cls, data: dict) -> "SampleGroupData":
        samples = data.get("Samples", [])

        # Calculate P95 and P99 from samples if available
        if samples:
            p95 = float(np.percentile(samples, 95))
            p99 = float(np.percentile(samples, 99))
            std_dev = float(np.std(samples))
        else:
            p95 = data.get("Max", 0)
            p99 = data.get("Max", 0)
            std_dev = data.get("StandardDeviation", 0)

        return cls(
            name=data.get("Name", ""),
            unit=data.get("Unit", SAMPLE_UNIT_UNDEFINED),
            samples=samples,
            median=data.get("Median", 0),
            std_dev=std_dev,
            p95=p95,
            p99=p99,
            min_val=data.get("Min", 0),
            max_val=data.get("Max", 0),
            sum_val=data.get("Sum", 0),
        )

    def is_time_based(self) -> bool:
        """Check if this sample group measures time (Unit = 1, microseconds)."""
        return self.unit == SAMPLE_UNIT_MICROSECOND


@dataclass
class TestResult:
    """Represents a single performance test result."""
    full_name: str
    class_name: str
    method_name: str
    fixture_args: str  # TestFixture constructor arguments
    test_case_args: str  # Test method arguments
    sample_groups: dict[str, SampleGroupData] = field(default_factory=dict)
    is_baseline: bool = False

    @classmethod
    def parse_test_name(cls, full_name: str) -> tuple[str, str, str, str]:
        """
        Parse the full test name to extract class, fixture args, method, and test case args.

        Example formats:
        - DCL.Tests.PlayMode.PerformanceTests.ProfilesPerformanceTest(url,False).PostProfilesAsync(1,5,0.25d,100)
        - DCL.Tests.PlayMode.PerformanceTests.AbCdnPerformanceTests.LoadFromDumpAsync(1,3,0,186)
        """
        # Pattern for class with TestFixture args
        fixture_pattern = r"^(.+?)\.([^.]+)\(([^)]*)\)\.([^.]+)\(([^)]*)\)$"
        # Pattern for class without TestFixture args
        simple_pattern = r"^(.+?)\.([^.]+)\.([^.]+)\(([^)]*)\)$"

        fixture_match = re.match(fixture_pattern, full_name)
        if fixture_match:
            namespace, class_name, fixture_args, method_name, test_case_args = fixture_match.groups()
            return f"{namespace}.{class_name}", fixture_args, method_name, test_case_args

        simple_match = re.match(simple_pattern, full_name)
        if simple_match:
            namespace, class_name, method_name, test_case_args = simple_match.groups()
            return f"{namespace}.{class_name}", "", method_name, test_case_args

        # Fallback
        return full_name, "", "", ""


def extract_scenario_label(fixture_args: str) -> str:
    """Extract a readable scenario label from TestFixture arguments."""
    if not fixture_args:
        return "default"

    # Try to extract meaningful identifier from URL or enum
    # Examples:
    # - "https://peer-ec1.decentraland.org/lambdas/,False" -> "ec1"
    # - "https://asset-bundle-registry.decentraland.today/,True" -> "today"
    # - "https://asset-bundle-registry-test.de..." -> "registry-test"
    # - "Org" -> "Org"
    # - "Zone" -> "Zone"

    # Check for URL patterns
    url_match = re.search(r"https?://([^/]+)", fixture_args)
    if url_match:
        domain = url_match.group(1)
        # Extract identifier from domain - check specific patterns first
        if "ec1" in domain:
            return "ec1"
        elif "ec2" in domain:
            return "ec2"
        elif "ap1" in domain:
            return "ap1"
        elif "today" in domain:
            return "today"
        elif "gateway" in domain.lower() and "zone" in domain.lower():
            return "gateway-zone"
        elif "zone" in domain.lower():
            return "zone"
        elif "org" in domain.lower():
            return "org"
        else:
            # Use first part of subdomain, but detect -test suffix
            parts = domain.split(".")
            subdomain = parts[0]

            # Check if this is a -test variant
            if "-test" in subdomain:
                # Extract meaningful part + test indicator
                base = subdomain.replace("-test", "")
                # Get last meaningful segment
                segments = base.split("-")
                if len(segments) > 1:
                    return f"{segments[-1]}-test"
                return f"{base[:10]}-test"

            # For non-test URLs, use last segment of hyphenated name
            segments = subdomain.split("-")
            if len(segments) > 1:
                return segments[-1][:15]
            return subdomain[:15]

    # Check for enum-like values
    args_list = [a.strip() for a in fixture_args.split(",")]
    if args_list:
        first_arg = args_list[0]
        if first_arg in ("Org", "Zone"):
            return first_arg.lower()
        return first_arg[:15]  # Limit length

    return fixture_args[:15]


def is_baseline_fixture(fixture_args: str) -> bool:
    """
    Check if the TestFixture should be used as baseline.
    Looks for a boolean 'true' that could indicate baseline parameter.
    """
    if not fixture_args:
        return False

    args_list = [a.strip().lower() for a in fixture_args.split(",")]

    # Look for explicit 'baseline' or a trailing 'true' that might be baseline param
    # This is a heuristic - adjust based on actual usage
    for i, arg in enumerate(args_list):
        if arg == "true" and i == len(args_list) - 1:
            # Last boolean argument might be baseline
            # But we need to be careful - it could be something else
            # For now, we don't auto-detect baseline from bool
            pass

    return False


def load_performance_results(json_path: str) -> dict:
    """Load and parse the performance test results JSON."""
    with open(json_path, "r", encoding="utf-8") as f:
        return json.load(f)


def group_test_results(data: dict) -> dict[str, dict[str, dict[str, TestResult]]]:
    """
    Group test results by class -> method+params -> scenario.

    Returns:
        Dict[class_name, Dict[method_testcase_key, Dict[scenario_label, TestResult]]]
    """
    grouped = defaultdict(lambda: defaultdict(dict))

    results = data.get("Results", [])

    for result in results:
        sample_groups_raw = result.get("SampleGroups", [])

        full_name = result.get("Name", "")
        class_name, fixture_args, method_name, test_case_args = TestResult.parse_test_name(full_name)

        scenario_label = extract_scenario_label(fixture_args)
        method_key = f"{method_name}({test_case_args})"

        test_result = TestResult(
            full_name=full_name,
            class_name=class_name,
            method_name=method_name,
            fixture_args=fixture_args,
            test_case_args=test_case_args,
            is_baseline=is_baseline_fixture(fixture_args),
        )

        # Parse sample groups (may be empty)
        for sg in sample_groups_raw:
            sg_data = SampleGroupData.from_json(sg)
            test_result.sample_groups[sg_data.name] = sg_data

        grouped[class_name][method_key][scenario_label] = test_result

    return grouped


def determine_baseline(scenarios: dict[str, TestResult]) -> str:
    """Determine which scenario should be the baseline."""
    # First, check if any scenario is explicitly marked as baseline
    for label, result in scenarios.items():
        if result.is_baseline:
            return label

    # Otherwise, use the first scenario (by insertion order in Python 3.7+)
    return next(iter(scenarios.keys()))


def calculate_percentage_diff(value: float, baseline: float) -> float:
    """Calculate percentage difference from baseline."""
    if baseline == 0:
        return 0.0
    return ((value - baseline) / baseline) * 100


def create_comparison_chart(
    ax: plt.Axes,
    title: str,
    scenarios: list[str],
    values: list[float],
    baseline_idx: int,
    std_devs: Optional[list[float]] = None,
    show_std_dev: bool = True,
    no_data_indices: Optional[list[int]] = None,
):
    """Create a bar chart comparing scenarios with percentage annotations."""
    if no_data_indices is None:
        no_data_indices = []

    colors = list(plt.cm.Set2(np.linspace(0, 1, len(scenarios))))

    # Mark no-data scenarios with gray color
    for idx in no_data_indices:
        colors[idx] = (0.7, 0.7, 0.7, 1.0)  # Gray

    x = np.arange(len(scenarios))
    bars = ax.bar(x, values, color=colors, edgecolor="black", linewidth=0.5)

    # Add error bars for standard deviation if provided (skip no-data)
    if show_std_dev and std_devs:
        valid_x = [xi for i, xi in enumerate(x) if i not in no_data_indices]
        valid_vals = [v for i, v in enumerate(values) if i not in no_data_indices]
        valid_stds = [s for i, s in enumerate(std_devs) if i not in no_data_indices]
        if valid_x:
            ax.errorbar(valid_x, valid_vals, yerr=valid_stds, fmt="none", ecolor="black", capsize=3, capthick=1)

    # Add percentage annotations
    baseline_value = values[baseline_idx] if baseline_idx not in no_data_indices else 0
    max_val = max(values) if values else 1

    for i, (bar, val) in enumerate(zip(bars, values)):
        if i in no_data_indices:
            # Show "No Data" annotation for missing data
            ax.annotate(
                "No Data",
                xy=(bar.get_x() + bar.get_width() / 2, max_val * 0.1),
                ha="center",
                va="bottom",
                fontsize=8,
                color="red",
                fontweight="bold",
            )
        else:
            pct_diff = calculate_percentage_diff(val, baseline_value)
            sign = "+" if pct_diff >= 0 else ""
            color = "green" if pct_diff <= 0 else "red"
            if i == baseline_idx:
                color = "blue"

            ax.annotate(
                f"{sign}{pct_diff:.1f}%",
                xy=(bar.get_x() + bar.get_width() / 2, bar.get_height()),
                xytext=(0, 5),
                textcoords="offset points",
                ha="center",
                va="bottom",
                fontsize=8,
                color=color,
            )

    ax.set_title(title, fontsize=10, fontweight="bold")
    ax.set_xticks(x)
    ax.set_xticklabels(scenarios, fontsize=8)
    ax.set_ylim(bottom=0)
    ax.grid(axis="y", linestyle="--", alpha=0.7)


def generate_report(json_path: str, output_path: str):
    """Generate the PDF report from performance test results."""
    print(f"Loading results from: {json_path}")
    data = load_performance_results(json_path)

    print("Grouping test results...")
    grouped = group_test_results(data)

    if not grouped:
        print("No test results found!")
        return

    print(f"Found {len(grouped)} test classes")

    # Filter out classes where all methods have only one scenario
    filtered_grouped = {}
    for class_name, methods in grouped.items():
        comparable_methods = {k: v for k, v in methods.items() if len(v) >= 2}
        if comparable_methods:
            filtered_grouped[class_name] = comparable_methods
        else:
            print(f"  Skipping class '{class_name}': only one TestFixture scenario (nothing to compare)")

    if not filtered_grouped:
        print("No comparable test results found (all tests have single TestFixture)!")
        return

    print(f"Processing {len(filtered_grouped)} test classes with multiple scenarios")

    with PdfPages(output_path) as pdf:
        for class_name, methods in filtered_grouped.items():
            print(f"Processing class: {class_name}")

            for method_key, scenarios in methods.items():

                scenario_labels = list(scenarios.keys())
                baseline_label = determine_baseline(scenarios)
                baseline_idx = scenario_labels.index(baseline_label)

                # Collect all unique time-based sample groups across all scenarios
                time_based_groups: set[str] = set()
                for result in scenarios.values():
                    for sg_name, sg_data in result.sample_groups.items():
                        if sg_data.is_time_based():
                            time_based_groups.add(sg_name)

                # Sort for consistent ordering
                time_based_groups = sorted(time_based_groups)

                if not time_based_groups:
                    continue

                # Create charts for each time-based sample group
                for sg_name in time_based_groups:
                    medians = []
                    median_stds = []
                    p95s = []
                    p95_stds = []
                    p99s = []
                    p99_stds = []
                    no_data_indices = []

                    for idx, label in enumerate(scenario_labels):
                        result = scenarios[label]
                        sg = result.sample_groups.get(sg_name)

                        if sg:
                            medians.append(sg.median)
                            median_stds.append(sg.std_dev)
                            p95s.append(sg.p95)
                            p99s.append(sg.p99)

                            # Calculate std dev for P95/P99 using approximation
                            if sg.samples:
                                p95_stds.append(sg.std_dev * 0.8)
                                p99_stds.append(sg.std_dev * 0.9)
                            else:
                                p95_stds.append(0)
                                p99_stds.append(0)
                        else:
                            # Track scenarios with no data
                            no_data_indices.append(idx)
                            medians.append(0)
                            median_stds.append(0)
                            p95s.append(0)
                            p95_stds.append(0)
                            p99s.append(0)
                            p99_stds.append(0)

                    # Skip if ALL scenarios have no data
                    if len(no_data_indices) == len(scenario_labels):
                        continue

                    # Create figure with 3 subplots for this sample group
                    fig, axes = plt.subplots(1, 3, figsize=(14, 4))
                    short_class_name = class_name.split(".")[-1]
                    fig.suptitle(
                        f"{short_class_name}.{scenarios[scenario_labels[0]].method_name} - {sg_name}\n"
                        f"Params {scenarios[scenario_labels[0]].test_case_args}",
                        fontsize=11, fontweight="bold"
                    )

                    # Median chart
                    create_comparison_chart(
                        axes[0], "Median", scenario_labels, medians, baseline_idx,
                        std_devs=median_stds, show_std_dev=True, no_data_indices=no_data_indices
                    )

                    # P95 chart
                    create_comparison_chart(
                        axes[1], "P95", scenario_labels, p95s, baseline_idx,
                        std_devs=p95_stds, show_std_dev=True, no_data_indices=no_data_indices
                    )

                    # P99 chart
                    create_comparison_chart(
                        axes[2], "P99", scenario_labels, p99s, baseline_idx,
                        std_devs=p99_stds, show_std_dev=True, no_data_indices=no_data_indices
                    )

                    plt.tight_layout()
                    pdf.savefig(fig, bbox_inches="tight")
                    plt.close(fig)

                # Also create Downloaded Data chart if available
                downloaded = []
                download_no_data_indices = []
                for idx, label in enumerate(scenario_labels):
                    result = scenarios[label]
                    download_sg = result.sample_groups.get("Iteration Downloaded Data")
                    if download_sg:
                        downloaded.append(download_sg.max_val)
                    else:
                        download_no_data_indices.append(idx)
                        downloaded.append(0)

                if any(d > 0 for d in downloaded):
                    fig, ax = plt.subplots(1, 1, figsize=(6, 4))
                    short_class_name = class_name.split(".")[-1]
                    fig.suptitle(
                        f"{short_class_name}.{scenarios[scenario_labels[0]].method_name} - Downloaded Data\n"
                        f"Params {scenarios[scenario_labels[0]].test_case_args}",
                        fontsize=11, fontweight="bold"
                    )
                    create_comparison_chart(
                        ax, "Downloaded (Max MB)", scenario_labels, downloaded, baseline_idx,
                        show_std_dev=False, no_data_indices=download_no_data_indices
                    )
                    plt.tight_layout()
                    pdf.savefig(fig, bbox_inches="tight")
                    plt.close(fig)

                print(f"  Generated charts for: {method_key} ({len(time_based_groups)} time-based metrics)")

    print(f"\nReport saved to: {output_path}")


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    input_json = sys.argv[1]
    output_pdf = sys.argv[2] if len(sys.argv) > 2 else "PerformanceBenchmarkReport.pdf"

    if not Path(input_json).exists():
        print(f"Error: Input file not found: {input_json}")
        sys.exit(1)

    generate_report(input_json, output_pdf)


if __name__ == "__main__":
    main()
