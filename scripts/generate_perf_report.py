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

Compare two reports:
    python generate_perf_report.py --compare <report1.json> <report2.json> <output.pdf> --fixture <TestFixtureName>
    python generate_perf_report.py --compare old.json new.json comparison.pdf --fixture ProfilesPerformanceTest
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


# Default config path (same directory as script)
DEFAULT_CONFIG_PATH = Path(__file__).parent / "perf_report_config.json"


def load_config(config_path: Path = DEFAULT_CONFIG_PATH) -> dict:
    """Load the report configuration file."""
    if not config_path.exists():
        print(f"Warning: Config file not found at {config_path}, using defaults")
        return {}
    with open(config_path, "r", encoding="utf-8") as f:
        return json.load(f)


def get_parallelism_category(concurrency: int, config: dict) -> tuple[str, str]:
    """
    Determine parallelism category based on concurrency value.

    Returns:
        Tuple of (category_key, human_readable_label)
    """
    parallelism = config.get("parallelism", {})

    for key, params in parallelism.items():
        min_val = params.get("min", 0)
        max_val = params.get("max")

        if max_val is None:
            if concurrency >= min_val:
                return key, params.get("label", key)
        elif min_val <= concurrency <= max_val:
            return key, params.get("label", key)

    return "unknown", "Unknown"


def get_difference_category(pct_diff: float, config: dict) -> tuple[str, str]:
    """
    Categorize a percentage difference based on thresholds.

    Returns:
        Tuple of (label, color)
    """
    thresholds = config.get("difference_thresholds", [])

    for threshold in thresholds:
        min_val = threshold.get("min")
        max_val = threshold.get("max")

        # Handle unbounded ranges
        if min_val is None and max_val is not None:
            if pct_diff < max_val:
                return threshold.get("label", ""), threshold.get("color", "#000000")
        elif max_val is None and min_val is not None:
            if pct_diff >= min_val:
                return threshold.get("label", ""), threshold.get("color", "#000000")
        elif min_val is not None and max_val is not None:
            if min_val <= pct_diff < max_val:
                return threshold.get("label", ""), threshold.get("color", "#000000")

    return "Unknown", "#000000"


def get_metric_display_name(metric_key: str, config: dict) -> tuple[str, str]:
    """
    Get human-readable name and description for a metric.

    Returns:
        Tuple of (name, description)
    """
    metrics = config.get("metrics", {})
    metric_info = metrics.get(metric_key, {})
    return (
        metric_info.get("name", metric_key),
        metric_info.get("description", metric_key.lower())
    )


def parse_test_case_args(test_case_args: str) -> dict:
    """
    Parse test case arguments to extract concurrency value.
    Assumes first argument is concurrency/parallelism count.

    Example: "10,50,0.25d,100" -> {"concurrency": 10, ...}
    """
    args = [a.strip() for a in test_case_args.split(",")]
    result = {}
    if args:
        try:
            result["concurrency"] = int(args[0])
        except ValueError:
            result["concurrency"] = 1
    return result


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
    p75: float
    p95: float
    p99: float
    min_val: float
    max_val: float
    sum_val: float

    @classmethod
    def from_json(cls, data: dict) -> "SampleGroupData":
        samples = data.get("Samples", [])

        # Calculate percentiles from samples if available
        if samples:
            p75 = float(np.percentile(samples, 75))
            p95 = float(np.percentile(samples, 95))
            p99 = float(np.percentile(samples, 99))
            std_dev = float(np.std(samples))
        else:
            p75 = data.get("Median", 0)
            p95 = data.get("Max", 0)
            p99 = data.get("Max", 0)
            std_dev = data.get("StandardDeviation", 0)

        return cls(
            name=data.get("Name", ""),
            unit=data.get("Unit", SAMPLE_UNIT_UNDEFINED),
            samples=samples,
            median=data.get("Median", 0),
            std_dev=std_dev,
            p75=p75,
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
        - DCL.Tests.PlayMode.PerformanceTests.AssetBundleRegistryOrgPerformanceTests(url).LoadFromDumpProdModeAsync
        """
        # Pattern for class with TestFixture args and method with params
        fixture_pattern = r"^(.+?)\.([^.]+)\(([^)]*)\)\.([^.]+)\(([^)]*)\)$"
        # Pattern for class with TestFixture args but method without params
        fixture_no_params_pattern = r"^(.+?)\.([^.]+)\(([^)]*)\)\.([^.]+)$"
        # Pattern for class without TestFixture args
        simple_pattern = r"^(.+?)\.([^.]+)\.([^.]+)\(([^)]*)\)$"

        fixture_match = re.match(fixture_pattern, full_name)
        if fixture_match:
            namespace, class_name, fixture_args, method_name, test_case_args = fixture_match.groups()
            return f"{namespace}.{class_name}", fixture_args, method_name, test_case_args

        fixture_no_params_match = re.match(fixture_no_params_pattern, full_name)
        if fixture_no_params_match:
            namespace, class_name, fixture_args, method_name = fixture_no_params_match.groups()
            return f"{namespace}.{class_name}", fixture_args, method_name, ""

        simple_match = re.match(simple_pattern, full_name)
        if simple_match:
            namespace, class_name, method_name, test_case_args = simple_match.groups()
            return f"{namespace}.{class_name}", "", method_name, test_case_args

        # Fallback
        return full_name, "", "", ""


def extract_scenario_label(fixture_args: str) -> str:
    """Extract a readable scenario label from TestFixture arguments.

    Extracts the full subdomain from URLs:
    - "https://peer-ap1.decentraland.org/..." -> "peer-ap1"
    - "https://asset-bundle-registry.decentraland.today/..." -> "asset-bundle-registry"
    - "https://gateway.decentraland.zone/..." -> "gateway.zone" (includes TLD for distinction)
    """
    if not fixture_args:
        return "default"

    # Check for URL patterns
    url_match = re.search(r"https?://([^/]+)", fixture_args)
    if url_match:
        domain = url_match.group(1)
        parts = domain.split(".")

        # Get subdomain (first part before the main domain)
        subdomain = parts[0]

        # For gateway URLs, include TLD to distinguish (e.g., gateway.zone vs gateway.org)
        if subdomain == "gateway" and len(parts) >= 3:
            tld = parts[-1]  # org, zone, today, etc.
            return f"{subdomain}.{tld}"

        return subdomain

    # Check for enum-like values
    args_list = [a.strip() for a in fixture_args.split(",")]
    if args_list:
        first_arg = args_list[0]
        if first_arg in ("Org", "Zone"):
            return first_arg.lower()
        return first_arg[:20]  # Limit length

    return fixture_args[:20]


def is_baseline_fixture(fixture_args: str) -> bool:
    """
    Check if the TestFixture should be used as baseline.
    Extracts the second parameter from fixture args - if it's 'True', this is the baseline.

    Example: "https://peer-ap1.decentraland.org/...",True,False -> baseline=True
    """
    if not fixture_args:
        return False

    # Split by comma, but be careful with URLs containing commas (though unlikely)
    # We need to handle quoted strings properly
    args_list = []
    current_arg = ""
    in_quotes = False

    for char in fixture_args:
        if char == '"':
            in_quotes = not in_quotes
            current_arg += char
        elif char == ',' and not in_quotes:
            args_list.append(current_arg.strip())
            current_arg = ""
        else:
            current_arg += char

    if current_arg:
        args_list.append(current_arg.strip())

    # Check if second parameter exists and is 'True'
    if len(args_list) >= 2:
        second_param = args_list[1].strip().lower()
        return second_param == "true"

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
            if i == baseline_idx:
                # Show "baseline" label for the baseline bar
                ax.annotate(
                    "baseline",
                    xy=(bar.get_x() + bar.get_width() / 2, bar.get_height()),
                    xytext=(0, 5),
                    textcoords="offset points",
                    ha="center",
                    va="bottom",
                    fontsize=8,
                    color="blue",
                    fontstyle="italic",
                )
            else:
                pct_diff = calculate_percentage_diff(val, baseline_value)
                sign = "+" if pct_diff >= 0 else ""
                color = "green" if pct_diff <= 0 else "red"

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

    # Rotate labels if any are long to prevent overlap
    max_label_len = max(len(s) for s in scenarios) if scenarios else 0
    if max_label_len > 12:
        ax.set_xticklabels(scenarios, fontsize=8, rotation=30, ha="right")
    else:
        ax.set_xticklabels(scenarios, fontsize=8)

    ax.set_ylim(bottom=0)
    ax.grid(axis="y", linestyle="--", alpha=0.7)


def generate_summary_page(
    pdf: PdfPages,
    grouped: dict,
    config: dict,
    all_grouped: Optional[dict] = None,
):
    """
    Generate a summary page showing key metrics for configured test cases.

    For each summary case, shows metrics across different parallelism levels.

    Args:
        grouped: Filtered grouped data (only classes with multiple scenarios)
        all_grouped: Unfiltered grouped data (includes single-scenario classes for list-only mode)
    """
    summary_cases = config.get("summary_cases", [])
    if not summary_cases:
        print("  No summary cases configured, skipping summary page")
        return

    metrics_config = config.get("metrics", {})
    default_metric_keys = config.get("default_summary_metrics", list(metrics_config.keys()))

    if not default_metric_keys:
        print("  No metrics configured, skipping summary page")
        return

    parallelism_config = config.get("parallelism", {})
    parallelism_order = ["no_concurrency", "low_concurrency", "high_concurrency"]

    # Collect summary data
    summary_data = []

    for case in summary_cases:
        test_name = case.get("test", "")
        endpoint = case.get("endpoint", "")
        compare_test = case.get("compare_test", "")
        # Use per-case metrics or fall back to default
        metric_keys = case.get("metrics", default_metric_keys)

        # Determine if this is a list-only case (no endpoint = no comparison needed)
        list_only_mode = not endpoint and not compare_test

        # Parse test name into class and method
        if "." in test_name:
            class_short, method_name = test_name.rsplit(".", 1)
        else:
            class_short = test_name
            method_name = ""

        # For list-only mode, look in all_grouped (unfiltered) first
        # For comparison mode, use filtered grouped data
        search_data = all_grouped if (list_only_mode and all_grouped) else grouped

        # Find matching class in grouped data
        matching_class = None
        for class_name in search_data.keys():
            if class_name.endswith(class_short):
                matching_class = class_name
                break

        if not matching_class:
            print(f"  Warning: No matching class found for '{test_name}'")
            continue

        methods = search_data[matching_class]

        # Find methods matching the method name
        matching_methods = {k: v for k, v in methods.items() if k.startswith(method_name + "(")}

        if not matching_methods:
            print(f"  Warning: No matching methods found for '{test_name}'")
            continue

        # If compare_test is specified, find the comparison test methods
        compare_methods = {}
        if compare_test:
            if "." in compare_test:
                compare_class_short, compare_method_name = compare_test.rsplit(".", 1)
            else:
                compare_class_short = compare_test
                compare_method_name = ""

            # Find matching comparison class
            compare_matching_class = None
            for class_name in grouped.keys():
                if class_name.endswith(compare_class_short):
                    compare_matching_class = class_name
                    break

            if compare_matching_class:
                compare_all_methods = grouped[compare_matching_class]
                compare_methods = {k: v for k, v in compare_all_methods.items()
                                   if k.startswith(compare_method_name + "(")}

        # Group by parallelism category
        case_results = {cat: [] for cat in parallelism_order}

        # Determine the mode based on whether endpoint is specified
        list_only_mode = not endpoint  # No comparison, just list metrics

        for method_key, scenarios in matching_methods.items():
            # Extract concurrency from method key
            # Use [^)]* to match zero or more chars (handles empty parentheses)
            args_match = re.search(r"\(([^)]*)\)", method_key)
            if args_match and args_match.group(1):
                parsed = parse_test_case_args(args_match.group(1))
                concurrency = parsed.get("concurrency", 1)
            else:
                # No parameters or empty parentheses -> default to 1 (no_concurrency)
                concurrency = 1

            cat_key, _ = get_parallelism_category(concurrency, config)

            if list_only_mode:
                # List-only mode: show metrics for all scenarios without comparison
                for scenario_label, result in scenarios.items():
                    if not result or not result.sample_groups:
                        continue

                    # Get total request count from WebRequest.Send samples
                    send_sg = result.sample_groups.get("WebRequest.Send")
                    total_requests = len(send_sg.samples) if send_sg else 0

                    for metric_key in metric_keys:
                        sg = result.sample_groups.get(metric_key)
                        if sg:
                            case_results[cat_key].append({
                                "metric": metric_key,
                                "scenario": scenario_label,
                                "median": sg.median,
                                "p75": sg.p75,
                                "p95": sg.p95,
                                "p99": sg.p99,
                                "unit": sg.unit,
                                "sum_val": sg.sum_val,
                                "sample_count": len(sg.samples),
                                "total_requests": total_requests,
                                "concurrency": concurrency,
                                "mode": "list",
                            })
                continue

            # The configured endpoint is our test subject
            if endpoint not in scenarios:
                continue

            subject_result = scenarios[endpoint]

            if not subject_result or not subject_result.sample_groups:
                continue

            if compare_test and compare_methods:
                # Cross-test comparison mode:
                # Compare subject (test.endpoint) against compare_test's endpoints

                # Find matching compare method by concurrency
                compare_method_key = None
                for ck in compare_methods.keys():
                    cargs_match = re.search(r"\(([^)]*)\)", ck)
                    if cargs_match and cargs_match.group(1):
                        cparsed = parse_test_case_args(cargs_match.group(1))
                        compare_concurrency = cparsed.get("concurrency", 1)
                    else:
                        # No parameters or empty parentheses -> default to 1
                        compare_concurrency = 1
                    if compare_concurrency == concurrency:
                        compare_method_key = ck
                        break

                if not compare_method_key:
                    continue

                compare_scenarios = compare_methods[compare_method_key]

                # Get total request count from WebRequest.Send samples
                subject_send_sg = subject_result.sample_groups.get("WebRequest.Send")
                subject_total_requests = len(subject_send_sg.samples) if subject_send_sg else 0

                # Compare against ALL endpoints in the compare test
                for compare_label, compare_result in compare_scenarios.items():
                    if not compare_result or not compare_result.sample_groups:
                        continue

                    for metric_key in metric_keys:
                        subject_sg = subject_result.sample_groups.get(metric_key)
                        compare_sg = compare_result.sample_groups.get(metric_key)

                        # Check if this is a failed_count metric
                        metric_config = config.get("metrics", {}).get(metric_key, {})
                        if metric_config.get("format") == "failed_count":
                            if subject_sg and subject_sg.sum_val > 0:
                                case_results[cat_key].append({
                                    "metric": metric_key,
                                    "failed_count": int(subject_sg.sum_val),
                                    "total_requests": subject_total_requests,
                                    "concurrency": concurrency,
                                    "mode": "failed_count",
                                })
                            continue

                        if subject_sg and compare_sg and compare_sg.median > 0:
                            # Calculate how subject compares to compare_test endpoints
                            # Negative = subject is faster (improvement)
                            # Positive = subject is slower (regression)
                            pct_diff = calculate_percentage_diff(subject_sg.median, compare_sg.median)
                            result_entry = {
                                "metric": metric_key,
                                "pct_diff": pct_diff,
                                "compare_endpoint": compare_label,
                                "subject_median": subject_sg.median,
                                "compare_median": compare_sg.median,
                                "concurrency": concurrency,
                            }
                            # Add P75 comparison if available
                            if compare_sg.p75 > 0:
                                result_entry["p75_diff"] = calculate_percentage_diff(subject_sg.p75, compare_sg.p75)
                            # Add P95 comparison if available
                            if compare_sg.p95 > 0:
                                result_entry["p95_diff"] = calculate_percentage_diff(subject_sg.p95, compare_sg.p95)
                            case_results[cat_key].append(result_entry)
            else:
                # Standard mode: Compare endpoint against other endpoints in same test
                other_endpoints = [label for label in scenarios.keys() if label != endpoint]

                if not other_endpoints:
                    continue

                # Get total request count from WebRequest.Send samples
                subject_send_sg = subject_result.sample_groups.get("WebRequest.Send")
                subject_total_requests = len(subject_send_sg.samples) if subject_send_sg else 0

                # Handle failed_count metrics first (only need to add once per category)
                failed_count_added = set()
                for metric_key in metric_keys:
                    metric_config = config.get("metrics", {}).get(metric_key, {})
                    if metric_config.get("format") == "failed_count" and metric_key not in failed_count_added:
                        subject_sg = subject_result.sample_groups.get(metric_key)
                        if subject_sg and subject_sg.sum_val > 0:
                            case_results[cat_key].append({
                                "metric": metric_key,
                                "failed_count": int(subject_sg.sum_val),
                                "total_requests": subject_total_requests,
                                "concurrency": concurrency,
                                "mode": "failed_count",
                            })
                            failed_count_added.add(metric_key)

                # Calculate metrics for each non-subject endpoint
                for other_label in other_endpoints:
                    other_result = scenarios[other_label]

                    if not other_result or not other_result.sample_groups:
                        continue

                    for metric_key in metric_keys:
                        # Skip failed_count metrics (already handled above)
                        metric_config = config.get("metrics", {}).get(metric_key, {})
                        if metric_config.get("format") == "failed_count":
                            continue

                        other_sg = other_result.sample_groups.get(metric_key)
                        subject_sg = subject_result.sample_groups.get(metric_key)

                        if other_sg and subject_sg and other_sg.median > 0:
                            # Calculate how subject compares to other (inverted)
                            # Negative = subject is faster (improvement)
                            # Positive = subject is slower (regression)
                            pct_diff = calculate_percentage_diff(subject_sg.median, other_sg.median)
                            result_entry = {
                                "metric": metric_key,
                                "pct_diff": pct_diff,
                                "other_endpoint": other_label,
                                "other_median": other_sg.median,
                                "subject_median": subject_sg.median,
                                "concurrency": concurrency,
                            }
                            # Add P75 comparison if available
                            if other_sg.p75 > 0:
                                result_entry["p75_diff"] = calculate_percentage_diff(subject_sg.p75, other_sg.p75)
                            # Add P95 comparison if available
                            if other_sg.p95 > 0:
                                result_entry["p95_diff"] = calculate_percentage_diff(subject_sg.p95, other_sg.p95)
                            case_results[cat_key].append(result_entry)

        summary_data.append({
            "test": test_name,
            "endpoint": endpoint,
            "compare_test": compare_test,
            "metric_keys": metric_keys,
            "results": case_results,
            "list_only_mode": list_only_mode,
        })

    # Create summary page
    fig = plt.figure(figsize=(11, 8.5))
    fig.suptitle("Performance Summary", fontsize=16, fontweight="bold", y=0.98)

    # Calculate layout
    y_pos = 0.92
    line_height = 0.025

    ax = fig.add_axes([0.05, 0.05, 0.9, 0.85])
    ax.axis("off")

    for case_data in summary_data:
        test_name = case_data["test"]
        endpoint = case_data["endpoint"]
        compare_test = case_data.get("compare_test", "")
        metric_keys = case_data.get("metric_keys", [])
        results = case_data["results"]
        list_only_mode = case_data.get("list_only_mode", False)

        # Test header
        ax.text(0.0, y_pos, f"{test_name}", fontsize=11, fontweight="bold",
                transform=ax.transAxes)

        if list_only_mode:
            # List-only mode: no comparison subtitle
            ax.text(0.0, y_pos - line_height,
                    "Metrics by scenario",
                    fontsize=9, fontstyle="italic", transform=ax.transAxes, color="#555555")
        elif compare_test:
            # Cross-test comparison mode
            ax.text(0.0, y_pos - line_height,
                    f"Endpoint: {endpoint} (vs {compare_test} endpoints)",
                    fontsize=9, fontstyle="italic", transform=ax.transAxes, color="#555555")
        else:
            # Standard mode
            ax.text(0.0, y_pos - line_height,
                    f"Endpoint: {endpoint} (performance vs other endpoints)",
                    fontsize=9, fontstyle="italic", transform=ax.transAxes, color="#555555")
        y_pos -= line_height * 2.5

        # Results by parallelism category
        for cat_key in parallelism_order:
            cat_label = parallelism_config.get(cat_key, {}).get("label", cat_key)
            cat_results = results.get(cat_key, [])

            if not cat_results:
                continue

            ax.text(0.02, y_pos, f"{cat_label}:", fontsize=10, fontweight="bold",
                    transform=ax.transAxes)
            y_pos -= line_height * 1.2

            if list_only_mode:
                # List-only mode: show metrics grouped by scenario
                # Group by scenario -> metric -> values
                scenario_metrics = defaultdict(lambda: defaultdict(list))
                scenario_failed = defaultdict(lambda: defaultdict(lambda: {"failed": 0, "total": 0}))
                for r in cat_results:
                    metric_config = config.get("metrics", {}).get(r["metric"], {})
                    if metric_config.get("format") == "failed_count":
                        scenario_failed[r["scenario"]][r["metric"]]["failed"] += r.get("sum_val", 0)
                        scenario_failed[r["scenario"]][r["metric"]]["total"] = r.get("total_requests", 0)
                    else:
                        scenario_metrics[r["scenario"]][r["metric"]].append({
                            "median": r["median"],
                            "p75": r["p75"],
                            "p95": r["p95"],
                            "unit": r["unit"],
                        })

                for scenario in sorted(set(list(scenario_metrics.keys()) + list(scenario_failed.keys()))):
                    ax.text(0.04, y_pos, f"{scenario}:", fontsize=9, fontweight="bold",
                            transform=ax.transAxes, color="#333333")
                    y_pos -= line_height

                    for metric_key in metric_keys:
                        metric_config = config.get("metrics", {}).get(metric_key, {})

                        # Handle failed_count format
                        if metric_config.get("format") == "failed_count":
                            failed_data = scenario_failed[scenario].get(metric_key)
                            if failed_data and failed_data["failed"] > 0:
                                text = f"    • Failed: {int(failed_data['failed'])}/{failed_data['total']}"
                                ax.text(0.06, y_pos, text, fontsize=8, transform=ax.transAxes,
                                        color="#8B0000")
                                y_pos -= line_height
                            continue

                        values = scenario_metrics[scenario].get(metric_key)
                        if not values:
                            continue

                        # Average the values if multiple
                        avg_median = sum(v["median"] for v in values) / len(values)
                        avg_p75 = sum(v["p75"] for v in values) / len(values)
                        avg_p95 = sum(v["p95"] for v in values) / len(values)
                        unit = values[0]["unit"]

                        metric_name, _ = get_metric_display_name(metric_key, config)

                        # Format based on unit (microseconds -> ms)
                        if unit == SAMPLE_UNIT_MICROSECOND:
                            median_ms = avg_median / 1000
                            text = f"    • {metric_name}: {median_ms:.1f}ms"
                            if metric_config.get("show_p75"):
                                text += f" (P75: {avg_p75 / 1000:.1f}ms)"
                            if metric_config.get("show_p95"):
                                text += f" (P95: {avg_p95 / 1000:.1f}ms)"
                        else:
                            text = f"    • {metric_name}: {avg_median:.2f}"
                            if metric_config.get("show_p75"):
                                text += f" (P75: {avg_p75:.2f})"
                            if metric_config.get("show_p95"):
                                text += f" (P95: {avg_p95:.2f})"

                        ax.text(0.06, y_pos, text, fontsize=8, transform=ax.transAxes,
                                color="#444444")
                        y_pos -= line_height

                    y_pos -= line_height * 0.3
            else:
                # Comparison mode: show percentage differences
                metric_averages = defaultdict(list)
                metric_p75_averages = defaultdict(list)
                metric_p95_averages = defaultdict(list)
                metric_failed = defaultdict(lambda: {"failed": 0, "total": 0})
                for r in cat_results:
                    if r.get("mode") == "failed_count":
                        metric_failed[r["metric"]]["failed"] += r.get("failed_count", 0)
                        metric_failed[r["metric"]]["total"] = r.get("total_requests", 0)
                    elif "pct_diff" in r:
                        metric_averages[r["metric"]].append(r["pct_diff"])
                        if "p75_diff" in r:
                            metric_p75_averages[r["metric"]].append(r["p75_diff"])
                        if "p95_diff" in r:
                            metric_p95_averages[r["metric"]].append(r["p95_diff"])

                # Iterate in the order specified by metric_keys
                for metric_key in metric_keys:
                    metric_config = config.get("metrics", {}).get(metric_key, {})

                    # Handle failed_count format
                    if metric_config.get("format") == "failed_count":
                        failed_data = metric_failed.get(metric_key)
                        if failed_data and failed_data["failed"] > 0:
                            text = f"  • Failed: {int(failed_data['failed'])}/{failed_data['total']}"
                            ax.text(0.04, y_pos, text, fontsize=9, transform=ax.transAxes,
                                    color="#8B0000")
                            y_pos -= line_height
                        continue

                    pct_diffs = metric_averages.get(metric_key)
                    if not pct_diffs:
                        continue
                    avg_diff = sum(pct_diffs) / len(pct_diffs)
                    metric_name, metric_desc = get_metric_display_name(metric_key, config)
                    diff_label, diff_color = get_difference_category(avg_diff, config)

                    sign = "+" if avg_diff >= 0 else ""
                    text = f"  • {metric_name}: {sign}{avg_diff:.1f}% ({diff_label})"

                    # Check if P75/P95 should be shown for this metric
                    p75_diffs = metric_p75_averages.get(metric_key)
                    p95_diffs = metric_p95_averages.get(metric_key)
                    if metric_config.get("show_p75") and p75_diffs:
                        avg_p75_diff = sum(p75_diffs) / len(p75_diffs)
                        p75_sign = "+" if avg_p75_diff >= 0 else ""
                        text += f" | P75: {p75_sign}{avg_p75_diff:.1f}%"
                    if metric_config.get("show_p95") and p95_diffs:
                        avg_p95_diff = sum(p95_diffs) / len(p95_diffs)
                        p95_sign = "+" if avg_p95_diff >= 0 else ""
                        text += f" | P95: {p95_sign}{avg_p95_diff:.1f}%"

                    ax.text(0.04, y_pos, text, fontsize=9, transform=ax.transAxes,
                            color=diff_color)
                    y_pos -= line_height

            y_pos -= line_height * 0.5

        y_pos -= line_height * 1.5

        # Check if we need a new page
        if y_pos < 0.1:
            pdf.savefig(fig, bbox_inches="tight")
            plt.close(fig)
            fig = plt.figure(figsize=(11, 8.5))
            fig.suptitle("Performance Summary (continued)", fontsize=16, fontweight="bold", y=0.98)
            ax = fig.add_axes([0.05, 0.05, 0.9, 0.85])
            ax.axis("off")
            y_pos = 0.92

    pdf.savefig(fig, bbox_inches="tight")
    plt.close(fig)
    print("  Generated summary page")

    return summary_data  # Return for GitHub summary generation


def generate_github_summary(summary_data: list, config: dict, output_path: str):
    """Generate a markdown summary for GitHub Actions."""
    parallelism_config = config.get("parallelism", {})
    parallelism_order = ["no_concurrency", "low_concurrency", "high_concurrency"]

    lines = ["## Performance Summary\n"]

    for case_data in summary_data:
        test_name = case_data["test"]
        endpoint = case_data["endpoint"]
        compare_test = case_data.get("compare_test", "")
        metric_keys = case_data.get("metric_keys", [])
        results = case_data["results"]
        list_only_mode = case_data.get("list_only_mode", False)

        # Test header
        lines.append(f"### {test_name}\n")

        if list_only_mode:
            lines.append("*Metrics by scenario*\n")
        elif compare_test:
            lines.append(f"*Endpoint: `{endpoint}` vs `{compare_test}` endpoints*\n")
        else:
            lines.append(f"*Endpoint: `{endpoint}` (performance vs other endpoints)*\n")

        # Check if there's any data
        has_data = any(results.get(cat) for cat in parallelism_order)
        if not has_data:
            lines.append("*No data available*\n")
            continue

        # Results by parallelism category
        for cat_key in parallelism_order:
            cat_label = parallelism_config.get(cat_key, {}).get("label", cat_key)
            cat_results = results.get(cat_key, [])

            if not cat_results:
                continue

            lines.append(f"**{cat_label}:**\n")

            if list_only_mode:
                # List-only mode: show metrics grouped by scenario
                scenario_metrics = defaultdict(lambda: defaultdict(list))
                scenario_failed = defaultdict(lambda: defaultdict(lambda: {"failed": 0, "total": 0}))
                for r in cat_results:
                    metric_config = config.get("metrics", {}).get(r["metric"], {})
                    if metric_config.get("format") == "failed_count":
                        scenario_failed[r["scenario"]][r["metric"]]["failed"] += r.get("sum_val", 0)
                        scenario_failed[r["scenario"]][r["metric"]]["total"] = r.get("total_requests", 0)
                    else:
                        scenario_metrics[r["scenario"]][r["metric"]].append({
                            "median": r["median"],
                            "p75": r["p75"],
                            "p95": r["p95"],
                            "unit": r["unit"],
                        })

                for scenario in sorted(set(list(scenario_metrics.keys()) + list(scenario_failed.keys()))):
                    lines.append(f"\n**{scenario}:**\n")

                    for metric_key in metric_keys:
                        metric_config = config.get("metrics", {}).get(metric_key, {})

                        # Handle failed_count format
                        if metric_config.get("format") == "failed_count":
                            failed_data = scenario_failed[scenario].get(metric_key)
                            if failed_data and failed_data["failed"] > 0:
                                lines.append(f"- 🔴 **Failed**: {int(failed_data['failed'])}/{failed_data['total']}\n")
                            continue

                        values = scenario_metrics[scenario].get(metric_key)
                        if not values:
                            continue

                        avg_median = sum(v["median"] for v in values) / len(values)
                        avg_p75 = sum(v["p75"] for v in values) / len(values)
                        avg_p95 = sum(v["p95"] for v in values) / len(values)
                        unit = values[0]["unit"]

                        metric_name, _ = get_metric_display_name(metric_key, config)

                        if unit == SAMPLE_UNIT_MICROSECOND:
                            median_ms = avg_median / 1000
                            text = f"- {metric_name}: {median_ms:.1f}ms"
                            if metric_config.get("show_p75"):
                                text += f" (P75: {avg_p75 / 1000:.1f}ms)"
                            if metric_config.get("show_p95"):
                                text += f" (P95: {avg_p95 / 1000:.1f}ms)"
                            lines.append(text + "\n")
                        else:
                            text = f"- {metric_name}: {avg_median:.2f}"
                            if metric_config.get("show_p75"):
                                text += f" (P75: {avg_p75:.2f})"
                            if metric_config.get("show_p95"):
                                text += f" (P95: {avg_p95:.2f})"
                            lines.append(text + "\n")
            else:
                # Comparison mode: show percentage differences
                metric_averages = defaultdict(list)
                metric_p75_averages = defaultdict(list)
                metric_p95_averages = defaultdict(list)
                metric_failed = defaultdict(lambda: {"failed": 0, "total": 0})
                for r in cat_results:
                    if r.get("mode") == "failed_count":
                        metric_failed[r["metric"]]["failed"] += r.get("failed_count", 0)
                        metric_failed[r["metric"]]["total"] = r.get("total_requests", 0)
                    elif "pct_diff" in r:
                        metric_averages[r["metric"]].append(r["pct_diff"])
                        if "p75_diff" in r:
                            metric_p75_averages[r["metric"]].append(r["p75_diff"])
                        if "p95_diff" in r:
                            metric_p95_averages[r["metric"]].append(r["p95_diff"])

                # Iterate in the order specified by metric_keys
                for metric_key in metric_keys:
                    metric_config = config.get("metrics", {}).get(metric_key, {})

                    # Handle failed_count format
                    if metric_config.get("format") == "failed_count":
                        failed_data = metric_failed.get(metric_key)
                        if failed_data and failed_data["failed"] > 0:
                            lines.append(f"- 🔴 **Failed**: {int(failed_data['failed'])}/{failed_data['total']}\n")
                        continue

                    pct_diffs = metric_averages.get(metric_key)
                    if not pct_diffs:
                        continue
                    avg_diff = sum(pct_diffs) / len(pct_diffs)
                    metric_name, _ = get_metric_display_name(metric_key, config)
                    diff_label, diff_color = get_difference_category(avg_diff, config)

                    sign = "+" if avg_diff >= 0 else ""

                    # Use emoji for visual indication
                    if "Improvement" in diff_label:
                        emoji = "🟢"
                    elif "Regression" in diff_label:
                        emoji = "🔴"
                    else:
                        emoji = "⚪"

                    text = f"- {emoji} **{metric_name}**: {sign}{avg_diff:.1f}% ({diff_label})"

                    # Check if P75/P95 should be shown for this metric
                    p75_diffs = metric_p75_averages.get(metric_key)
                    p95_diffs = metric_p95_averages.get(metric_key)
                    if metric_config.get("show_p75") and p75_diffs:
                        avg_p75_diff = sum(p75_diffs) / len(p75_diffs)
                        p75_sign = "+" if avg_p75_diff >= 0 else ""
                        text += f" | P75: {p75_sign}{avg_p75_diff:.1f}%"
                    if metric_config.get("show_p95") and p95_diffs:
                        avg_p95_diff = sum(p95_diffs) / len(p95_diffs)
                        p95_sign = "+" if avg_p95_diff >= 0 else ""
                        text += f" | P95: {p95_sign}{avg_p95_diff:.1f}%"

                    lines.append(text + "\n")

            lines.append("\n")

        lines.append("---\n")

    # Write to file
    with open(output_path, "w", encoding="utf-8") as f:
        f.writelines(lines)

    print(f"  Generated GitHub summary: {output_path}")


def create_comparison_chart_dual_report(
    ax: plt.Axes,
    title: str,
    scenarios: list[str],
    values1: list[float],
    values2: list[float],
    baseline_idx: int,
    std_devs1: Optional[list[float]] = None,
    std_devs2: Optional[list[float]] = None,
    show_std_dev: bool = True,
    no_data_indices1: Optional[list[int]] = None,
    no_data_indices2: Optional[list[int]] = None,
    report1_label: str = "Report 1",
    report2_label: str = "Report 2",
):
    """Create a grouped bar chart comparing two reports for the same scenarios."""
    if no_data_indices1 is None:
        no_data_indices1 = []
    if no_data_indices2 is None:
        no_data_indices2 = []

    n = len(scenarios)
    x = np.arange(n)
    width = 0.35

    # Colors for each report
    color1 = "#1f77b4"  # Blue for Report 1
    color2 = "#ff7f0e"  # Orange for Report 2

    # Create bars
    bars1 = ax.bar(x - width/2, values1, width, label=report1_label, color=color1, edgecolor="black", linewidth=0.5)
    bars2 = ax.bar(x + width/2, values2, width, label=report2_label, color=color2, edgecolor="black", linewidth=0.5)

    # Mark no-data bars with gray
    for idx in no_data_indices1:
        bars1[idx].set_color((0.7, 0.7, 0.7, 1.0))
    for idx in no_data_indices2:
        bars2[idx].set_color((0.7, 0.7, 0.7, 1.0))

    # Add error bars if provided
    if show_std_dev and std_devs1:
        valid_x1 = [xi - width/2 for i, xi in enumerate(x) if i not in no_data_indices1]
        valid_vals1 = [v for i, v in enumerate(values1) if i not in no_data_indices1]
        valid_stds1 = [s for i, s in enumerate(std_devs1) if i not in no_data_indices1]
        if valid_x1:
            ax.errorbar(valid_x1, valid_vals1, yerr=valid_stds1, fmt="none", ecolor="black", capsize=2, capthick=1)

    if show_std_dev and std_devs2:
        valid_x2 = [xi + width/2 for i, xi in enumerate(x) if i not in no_data_indices2]
        valid_vals2 = [v for i, v in enumerate(values2) if i not in no_data_indices2]
        valid_stds2 = [s for i, s in enumerate(std_devs2) if i not in no_data_indices2]
        if valid_x2:
            ax.errorbar(valid_x2, valid_vals2, yerr=valid_stds2, fmt="none", ecolor="black", capsize=2, capthick=1)

    # Use baseline from Report 1 for percentage calculations
    baseline_value = values1[baseline_idx] if baseline_idx not in no_data_indices1 else 0
    max_val = max(max(values1) if values1 else 1, max(values2) if values2 else 1)

    # Annotate Report 1 bars
    for i, (bar, val) in enumerate(zip(bars1, values1)):
        if i in no_data_indices1:
            ax.annotate("N/A", xy=(bar.get_x() + bar.get_width() / 2, max_val * 0.05),
                        ha="center", va="bottom", fontsize=7, color="red")
        elif i == baseline_idx:
            ax.annotate("base", xy=(bar.get_x() + bar.get_width() / 2, bar.get_height()),
                        xytext=(0, 3), textcoords="offset points", ha="center", va="bottom",
                        fontsize=7, color="blue", fontstyle="italic")

    # Annotate Report 2 bars (compare to Report 1 baseline)
    for i, (bar, val) in enumerate(zip(bars2, values2)):
        if i in no_data_indices2:
            ax.annotate("N/A", xy=(bar.get_x() + bar.get_width() / 2, max_val * 0.05),
                        ha="center", va="bottom", fontsize=7, color="red")
        elif baseline_value > 0:
            pct_diff = calculate_percentage_diff(val, baseline_value)
            sign = "+" if pct_diff >= 0 else ""
            color = "green" if pct_diff <= 0 else "red"
            ax.annotate(f"{sign}{pct_diff:.1f}%", xy=(bar.get_x() + bar.get_width() / 2, bar.get_height()),
                        xytext=(0, 3), textcoords="offset points", ha="center", va="bottom",
                        fontsize=7, color=color)

    ax.set_title(title, fontsize=10, fontweight="bold")
    ax.set_xticks(x)

    max_label_len = max(len(s) for s in scenarios) if scenarios else 0
    if max_label_len > 10:
        ax.set_xticklabels(scenarios, fontsize=7, rotation=30, ha="right")
    else:
        ax.set_xticklabels(scenarios, fontsize=8)

    ax.set_ylim(bottom=0)
    ax.grid(axis="y", linestyle="--", alpha=0.7)
    ax.legend(loc="upper right", fontsize=8)


def generate_comparison_report(
    json_path1: str,
    json_path2: str,
    output_path: str,
    fixture_filter: str,
    config_path: Optional[Path] = None,
    report1_label: str = "Report 1",
    report2_label: str = "Report 2",
):
    """Generate a PDF comparing two performance reports for a specific test fixture."""
    # Load configuration
    config = load_config(config_path) if config_path else load_config()

    print(f"Comparing reports for fixture: {fixture_filter}")
    print(f"  Report 1: {json_path1}")
    print(f"  Report 2: {json_path2}")

    # Load both reports
    data1 = load_performance_results(json_path1)
    data2 = load_performance_results(json_path2)

    # Group results
    grouped1 = group_test_results(data1)
    grouped2 = group_test_results(data2)

    # Find matching classes for the fixture filter
    matching_class1 = None
    matching_class2 = None

    for class_name in grouped1.keys():
        if fixture_filter in class_name:
            matching_class1 = class_name
            break

    for class_name in grouped2.keys():
        if fixture_filter in class_name:
            matching_class2 = class_name
            break

    if not matching_class1:
        print(f"Error: No matching class found for '{fixture_filter}' in Report 1")
        return

    if not matching_class2:
        print(f"Error: No matching class found for '{fixture_filter}' in Report 2")
        return

    methods1 = grouped1[matching_class1]
    methods2 = grouped2[matching_class2]

    # Filter to comparable methods (multiple scenarios in at least one report)
    all_method_keys = set(methods1.keys()) | set(methods2.keys())

    print(f"Found {len(all_method_keys)} methods to compare")

    with PdfPages(output_path) as pdf:
        for method_key in sorted(all_method_keys):
            scenarios1 = methods1.get(method_key, {})
            scenarios2 = methods2.get(method_key, {})

            # Get all scenario labels from both reports
            all_scenarios = sorted(set(scenarios1.keys()) | set(scenarios2.keys()))

            if len(all_scenarios) < 1:
                continue

            # Determine baseline from Report 1
            if scenarios1:
                baseline_label = determine_baseline(scenarios1)
            elif scenarios2:
                baseline_label = determine_baseline(scenarios2)
            else:
                continue

            if baseline_label not in all_scenarios:
                baseline_label = all_scenarios[0]

            baseline_idx = all_scenarios.index(baseline_label)

            # Collect all unique time-based sample groups
            time_based_groups: set[str] = set()
            for result in list(scenarios1.values()) + list(scenarios2.values()):
                for sg_name, sg_data in result.sample_groups.items():
                    if sg_data.is_time_based():
                        time_based_groups.add(sg_name)

            time_based_groups = sorted(time_based_groups)

            if not time_based_groups:
                continue

            # Create charts for each sample group
            for sg_name in time_based_groups:
                medians1, median_stds1, no_data1 = [], [], []
                medians2, median_stds2, no_data2 = [], [], []
                p95s1, p95_stds1 = [], []
                p95s2, p95_stds2 = [], []
                p99s1, p99_stds1 = [], []
                p99s2, p99_stds2 = [], []

                for idx, label in enumerate(all_scenarios):
                    # Report 1 data
                    result1 = scenarios1.get(label)
                    sg1 = result1.sample_groups.get(sg_name) if result1 else None

                    if sg1:
                        medians1.append(sg1.median)
                        median_stds1.append(sg1.std_dev)
                        p95s1.append(sg1.p95)
                        p99s1.append(sg1.p99)
                        p95_stds1.append(sg1.std_dev * 0.8 if sg1.samples else 0)
                        p99_stds1.append(sg1.std_dev * 0.9 if sg1.samples else 0)
                    else:
                        no_data1.append(idx)
                        medians1.append(0)
                        median_stds1.append(0)
                        p95s1.append(0)
                        p99s1.append(0)
                        p95_stds1.append(0)
                        p99_stds1.append(0)

                    # Report 2 data
                    result2 = scenarios2.get(label)
                    sg2 = result2.sample_groups.get(sg_name) if result2 else None

                    if sg2:
                        medians2.append(sg2.median)
                        median_stds2.append(sg2.std_dev)
                        p95s2.append(sg2.p95)
                        p99s2.append(sg2.p99)
                        p95_stds2.append(sg2.std_dev * 0.8 if sg2.samples else 0)
                        p99_stds2.append(sg2.std_dev * 0.9 if sg2.samples else 0)
                    else:
                        no_data2.append(idx)
                        medians2.append(0)
                        median_stds2.append(0)
                        p95s2.append(0)
                        p99s2.append(0)
                        p95_stds2.append(0)
                        p99_stds2.append(0)

                # Skip if both reports have no data
                if len(no_data1) == len(all_scenarios) and len(no_data2) == len(all_scenarios):
                    continue

                # Create figure with 3 subplots
                fig, axes = plt.subplots(1, 3, figsize=(15, 5))
                short_class = matching_class1.split(".")[-1]

                # Get test case args from whichever report has data
                sample_result = next(iter(scenarios1.values()), None) or next(iter(scenarios2.values()), None)
                test_case_args = sample_result.test_case_args if sample_result else ""

                fig.suptitle(
                    f"{short_class}.{sample_result.method_name if sample_result else method_key} - {sg_name}\n"
                    f"Params: {test_case_args}",
                    fontsize=11, fontweight="bold"
                )

                # Median chart
                create_comparison_chart_dual_report(
                    axes[0], "Median", all_scenarios, medians1, medians2, baseline_idx,
                    std_devs1=median_stds1, std_devs2=median_stds2, show_std_dev=True,
                    no_data_indices1=no_data1, no_data_indices2=no_data2,
                    report1_label=report1_label, report2_label=report2_label
                )

                # P95 chart
                create_comparison_chart_dual_report(
                    axes[1], "P95", all_scenarios, p95s1, p95s2, baseline_idx,
                    std_devs1=p95_stds1, std_devs2=p95_stds2, show_std_dev=True,
                    no_data_indices1=no_data1, no_data_indices2=no_data2,
                    report1_label=report1_label, report2_label=report2_label
                )

                # P99 chart
                create_comparison_chart_dual_report(
                    axes[2], "P99", all_scenarios, p99s1, p99s2, baseline_idx,
                    std_devs1=p99_stds1, std_devs2=p99_stds2, show_std_dev=True,
                    no_data_indices1=no_data1, no_data_indices2=no_data2,
                    report1_label=report1_label, report2_label=report2_label
                )

                plt.tight_layout()
                pdf.savefig(fig, bbox_inches="tight")
                plt.close(fig)

            print(f"  Generated comparison charts for: {method_key}")

    print(f"\nComparison report saved to: {output_path}")


def generate_report(
    json_path: str,
    output_path: str,
    config_path: Optional[Path] = None,
    summary_only: bool = False,
    github_summary_path: Optional[str] = None,
):
    """Generate the PDF report from performance test results."""
    # Load configuration
    if config_path:
        config = load_config(config_path)
    else:
        config = load_config()

    if summary_only:
        print("Summary-only mode enabled")

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

    if not filtered_grouped and not grouped:
        print("No test results found!")
        return

    print(f"Processing {len(filtered_grouped)} test classes with multiple scenarios")

    with PdfPages(output_path) as pdf:
        # Generate summary page first (pass both filtered and unfiltered for list-only cases)
        print("Generating summary page...")
        summary_data = generate_summary_page(pdf, filtered_grouped, config, all_grouped=grouped)

        # Generate GitHub Actions summary if requested
        if github_summary_path and summary_data:
            generate_github_summary(summary_data, config, github_summary_path)

        if summary_only:
            print(f"\nSummary-only report saved to: {output_path}")
            return

        # Generate detailed charts for each class
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

                print(f"  Generated charts for: {method_key} (baseline: {baseline_label}, {len(time_based_groups)} time-based metrics)")

    print(f"\nReport saved to: {output_path}")


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        print("\nOptions:")
        print("  --summary-only              Generate only the summary page (useful for debugging)")
        print("  --github-summary <path>     Output markdown summary for GitHub Actions")
        print("\nCompare mode:")
        print("  --compare <report1.json> <report2.json> <output.pdf> --fixture <name>")
        print("  --label1 <name>             Label for first report (default: 'Report 1')")
        print("  --label2 <name>             Label for second report (default: 'Report 2')")
        sys.exit(1)

    # Parse arguments
    args = sys.argv[1:]

    # Check for compare mode
    if "--compare" in args:
        args.remove("--compare")

        # Parse fixture filter
        fixture_filter = None
        if "--fixture" in args:
            idx = args.index("--fixture")
            if idx + 1 < len(args):
                fixture_filter = args[idx + 1]
                args.pop(idx)
                args.pop(idx)
            else:
                print("Error: --fixture requires a name argument")
                sys.exit(1)
        else:
            print("Error: --fixture is required for compare mode")
            sys.exit(1)

        # Parse optional labels
        report1_label = "Report 1"
        report2_label = "Report 2"

        if "--label1" in args:
            idx = args.index("--label1")
            if idx + 1 < len(args):
                report1_label = args[idx + 1]
                args.pop(idx)
                args.pop(idx)

        if "--label2" in args:
            idx = args.index("--label2")
            if idx + 1 < len(args):
                report2_label = args[idx + 1]
                args.pop(idx)
                args.pop(idx)

        if len(args) < 3:
            print("Error: Compare mode requires <report1.json> <report2.json> <output.pdf>")
            sys.exit(1)

        json_path1, json_path2, output_pdf = args[0], args[1], args[2]

        if not Path(json_path1).exists():
            print(f"Error: Report 1 not found: {json_path1}")
            sys.exit(1)
        if not Path(json_path2).exists():
            print(f"Error: Report 2 not found: {json_path2}")
            sys.exit(1)

        generate_comparison_report(
            json_path1, json_path2, output_pdf, fixture_filter,
            report1_label=report1_label, report2_label=report2_label
        )
        return

    # Standard single-report mode
    summary_only = "--summary-only" in args
    if summary_only:
        args.remove("--summary-only")

    github_summary_path = None
    if "--github-summary" in args:
        idx = args.index("--github-summary")
        if idx + 1 < len(args):
            github_summary_path = args[idx + 1]
            args.pop(idx)  # Remove --github-summary
            args.pop(idx)  # Remove the path
        else:
            print("Error: --github-summary requires a path argument")
            sys.exit(1)

    if not args:
        print("Error: Input JSON file required")
        sys.exit(1)

    input_json = args[0]
    output_pdf = args[1] if len(args) > 1 else "PerformanceBenchmarkReport.pdf"

    if not Path(input_json).exists():
        print(f"Error: Input file not found: {input_json}")
        sys.exit(1)

    generate_report(input_json, output_pdf, summary_only=summary_only, github_summary_path=github_summary_path)


if __name__ == "__main__":
    main()
