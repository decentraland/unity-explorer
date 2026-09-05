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
"""

import argparse
import json
import re
import sys
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Optional

import matplotlib.pyplot as plt
import numpy as np
from matplotlib.backends.backend_pdf import PdfPages

# =============================================================================
# Constants
# =============================================================================

SAMPLE_UNIT_UNDEFINED = 0
SAMPLE_UNIT_MICROSECOND = 1
SAMPLE_UNIT_MILLISECOND = 2
SAMPLE_UNIT_SECOND = 3
SAMPLE_UNIT_BYTE = 4
SAMPLE_UNIT_KILOBYTE = 5
SAMPLE_UNIT_MEGABYTE = 6
SAMPLE_UNIT_GIGABYTE = 7
SAMPLE_UNIT_NANOSECOND = 8

PARALLELISM_ORDER = ["no_concurrency", "low_concurrency", "high_concurrency"]
DEFAULT_CONFIG_PATH = Path(__file__).parent / "perf_report_config.json"

# =============================================================================
# Configuration Helpers
# =============================================================================


def load_config(config_path: Path = DEFAULT_CONFIG_PATH) -> dict:
    """Load the report configuration file."""
    if not config_path.exists():
        print(f"Warning: Config file not found at {config_path}, using defaults")
        return {}
    with open(config_path, "r", encoding="utf-8") as f:
        return json.load(f)


def get_parallelism_category(concurrency: int, config: dict) -> tuple[str, str]:
    """Determine parallelism category. Returns (category_key, label)."""
    for key, params in config.get("parallelism", {}).items():
        min_val, max_val = params.get("min", 0), params.get("max")
        if max_val is None:
            if concurrency >= min_val:
                return key, params.get("label", key)
        elif min_val <= concurrency <= max_val:
            return key, params.get("label", key)
    return "unknown", "Unknown"


def get_difference_category(pct_diff: float, config: dict) -> tuple[str, str]:
    """Categorize a percentage difference. Returns (label, color)."""
    for t in config.get("difference_thresholds", []):
        min_val, max_val = t.get("min"), t.get("max")
        if min_val is None and max_val is not None and pct_diff < max_val:
            return t.get("label", ""), t.get("color", "#000000")
        if max_val is None and min_val is not None and pct_diff >= min_val:
            return t.get("label", ""), t.get("color", "#000000")
        if min_val is not None and max_val is not None and min_val <= pct_diff < max_val:
            return t.get("label", ""), t.get("color", "#000000")
    return "Unknown", "#000000"


def get_metric_config(metric_key: str, config: dict) -> dict:
    """Get metric configuration dict."""
    return config.get("metrics", {}).get(metric_key, {})


def get_metric_display_name(metric_key: str, config: dict) -> tuple[str, str]:
    """Get (name, description) for a metric."""
    info = get_metric_config(metric_key, config)
    return info.get("name", metric_key), info.get("description", metric_key.lower())


# =============================================================================
# Formatting Helpers
# =============================================================================


def format_sign(value: float) -> str:
    """Return '+' for non-negative, empty for negative."""
    return "+" if value >= 0 else ""


def format_pct(value: float) -> str:
    """Format percentage with sign."""
    return f"{format_sign(value)}{value:.1f}%"


def format_metric_value(value: float, unit: int, show_unit: bool = True) -> str:
    """Format a metric value based on its unit."""
    if unit == SAMPLE_UNIT_MICROSECOND:
        return f"{value / 1000:.1f}ms" if show_unit else f"{value / 1000:.1f}"
    return f"{value:.2f}"


def format_percentile_suffix(
    metric_key: str, config: dict, p75: Optional[float] = None, p95: Optional[float] = None, unit: int = 0
) -> str:
    """Build P75/P95 suffix string if configured for this metric."""
    mc = get_metric_config(metric_key, config)
    parts = []
    if mc.get("show_p75") and p75 is not None:
        parts.append(f"P75: {format_metric_value(p75, unit)}")
    if mc.get("show_p95") and p95 is not None:
        parts.append(f"P95: {format_metric_value(p95, unit)}")
    return f" ({') ('.join(parts)})" if parts else ""


def format_percentile_diff_suffix(
    metric_key: str, config: dict, p75_diff: Optional[float] = None, p95_diff: Optional[float] = None
) -> str:
    """Build P75/P95 diff suffix string."""
    mc = get_metric_config(metric_key, config)
    parts = []
    if mc.get("show_p75") and p75_diff is not None:
        parts.append(f"P75: {format_pct(p75_diff)}")
    if mc.get("show_p95") and p95_diff is not None:
        parts.append(f"P95: {format_pct(p95_diff)}")
    return " | " + " | ".join(parts) if parts else ""


def get_diff_emoji(label: str) -> str:
    """Get emoji indicator for difference label."""
    if "Improvement" in label:
        return "\U0001F7E2"  # Green circle
    if "Regression" in label:
        return "\U0001F534"  # Red circle
    return "\u26AA"  # White circle


def avg(values: list[float]) -> float:
    """Calculate average, returning 0 for empty list."""
    return sum(values) / len(values) if values else 0.0


# =============================================================================
# Data Classes
# =============================================================================


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
        if samples:
            p75, p95, p99 = (float(np.percentile(samples, p)) for p in (75, 95, 99))
            std_dev = float(np.std(samples))
        else:
            p75, p95, p99 = data.get("Median", 0), data.get("Max", 0), data.get("Max", 0)
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
        return self.unit == SAMPLE_UNIT_MICROSECOND


@dataclass
class TestResult:
    """Represents a single performance test result."""
    full_name: str
    class_name: str
    method_name: str
    fixture_args: str
    test_case_args: str
    sample_groups: dict[str, SampleGroupData] = field(default_factory=dict)
    is_baseline: bool = False

    @classmethod
    def parse_test_name(cls, full_name: str) -> tuple[str, str, str, str]:
        """Parse full test name to (class_name, fixture_args, method_name, test_case_args)."""
        patterns = [
            (r"^(.+?)\.([^.]+)\(([^)]*)\)\.([^.]+)\(([^)]*)\)$", lambda m: (f"{m[0]}.{m[1]}", m[2], m[3], m[4])),
            (r"^(.+?)\.([^.]+)\(([^)]*)\)\.([^.]+)$", lambda m: (f"{m[0]}.{m[1]}", m[2], m[3], "")),
            (r"^(.+?)\.([^.]+)\.([^.]+)\(([^)]*)\)$", lambda m: (f"{m[0]}.{m[1]}", "", m[2], m[3])),
        ]
        for pattern, extractor in patterns:
            match = re.match(pattern, full_name)
            if match:
                return extractor(match.groups())
        return full_name, "", "", ""


# =============================================================================
# Test Name Parsing Helpers
# =============================================================================


def extract_scenario_label(fixture_args: str) -> str:
    """Extract readable scenario label from TestFixture arguments."""
    if not fixture_args:
        return "default"

    url_match = re.search(r"https?://([^/]+)", fixture_args)
    if url_match:
        parts = url_match.group(1).split(".")
        subdomain = parts[0]
        if subdomain == "gateway" and len(parts) >= 3:
            return f"{subdomain}.{parts[-1]}"
        return subdomain

    args_list = [a.strip() for a in fixture_args.split(",")]
    if args_list:
        first = args_list[0]
        if first in ("Org", "Zone"):
            return first.lower()
        return first[:20]
    return fixture_args[:20]


def is_baseline_fixture(fixture_args: str) -> bool:
    """Check if TestFixture should be baseline (second param is 'True')."""
    if not fixture_args:
        return False

    # Parse comma-separated args respecting quotes
    args_list, current, in_quotes = [], "", False
    for char in fixture_args:
        if char == '"':
            in_quotes = not in_quotes
            current += char
        elif char == ',' and not in_quotes:
            args_list.append(current.strip())
            current = ""
        else:
            current += char
    if current:
        args_list.append(current.strip())

    return len(args_list) >= 2 and args_list[1].strip().lower() == "true"


def parse_test_case_args(test_case_args: str) -> dict:
    """Parse test case arguments. First arg is concurrency."""
    args = [a.strip() for a in test_case_args.split(",")]
    if args:
        try:
            return {"concurrency": int(args[0])}
        except ValueError:
            pass
    return {"concurrency": 1}


def extract_concurrency(method_key: str) -> int:
    """Extract concurrency from method key like 'Method(1,5,0.25d)'."""
    match = re.search(r"\(([^)]*)\)", method_key)
    if match and match.group(1):
        return parse_test_case_args(match.group(1)).get("concurrency", 1)
    return 1


# =============================================================================
# Data Loading and Grouping
# =============================================================================


def load_performance_results(json_path: str) -> dict:
    """Load performance test results JSON."""
    with open(json_path, "r", encoding="utf-8") as f:
        return json.load(f)


def group_test_results(data: dict) -> dict[str, dict[str, dict[str, TestResult]]]:
    """Group results by class -> method+params -> scenario."""
    grouped = defaultdict(lambda: defaultdict(dict))

    for result in data.get("Results", []):
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

        for sg in result.get("SampleGroups", []):
            sg_data = SampleGroupData.from_json(sg)
            test_result.sample_groups[sg_data.name] = sg_data

        grouped[class_name][method_key][scenario_label] = test_result

    return grouped


def determine_baseline(scenarios: dict[str, TestResult]) -> str:
    """Determine which scenario is baseline."""
    for label, result in scenarios.items():
        if result.is_baseline:
            return label
    return next(iter(scenarios.keys()))


def calculate_percentage_diff(value: float, baseline: float) -> float:
    """Calculate percentage difference from baseline."""
    return ((value - baseline) / baseline) * 100 if baseline != 0 else 0.0


# =============================================================================
# Summary Data Collection
# =============================================================================


def collect_list_only_metrics(
    result: TestResult, metric_keys: list[str], config: dict, concurrency: int
) -> list[dict]:
    """Collect metrics for list-only mode (no comparison)."""
    items = []
    if not result or not result.sample_groups:
        return items

    send_sg = result.sample_groups.get("WebRequest.Send")
    total_requests = len(send_sg.samples) if send_sg else 0

    for metric_key in metric_keys:
        sg = result.sample_groups.get(metric_key)
        if not sg:
            continue

        mc = get_metric_config(metric_key, config)
        if mc.get("format") == "failed_count":
            if sg.sum_val > 0:
                items.append({
                    "metric": metric_key, "mode": "failed_count",
                    "failed_count": int(sg.sum_val), "total_requests": total_requests,
                })
        else:
            items.append({
                "metric": metric_key, "mode": "list",
                "median": sg.median, "p75": sg.p75, "p95": sg.p95, "p99": sg.p99,
                "unit": sg.unit, "sum_val": sg.sum_val,
                "sample_count": len(sg.samples), "total_requests": total_requests,
                "concurrency": concurrency, "scenario": extract_scenario_label(result.fixture_args),
            })
    return items


def collect_comparison_metrics(
    subject: TestResult, baseline: TestResult, metric_keys: list[str], config: dict, concurrency: int
) -> list[dict]:
    """Collect metrics comparing subject to baseline."""
    items = []
    if not subject or not baseline or not subject.sample_groups:
        return items

    send_sg = subject.sample_groups.get("WebRequest.Send")
    total_requests = len(send_sg.samples) if send_sg else 0

    for metric_key in metric_keys:
        mc = get_metric_config(metric_key, config)
        subject_sg = subject.sample_groups.get(metric_key)
        baseline_sg = baseline.sample_groups.get(metric_key)

        if mc.get("format") == "failed_count":
            if subject_sg and subject_sg.sum_val > 0:
                items.append({
                    "metric": metric_key, "mode": "failed_count",
                    "failed_count": int(subject_sg.sum_val), "total_requests": total_requests,
                })
            continue

        if subject_sg and baseline_sg and baseline_sg.median > 0:
            entry = {
                "metric": metric_key,
                "pct_diff": calculate_percentage_diff(subject_sg.median, baseline_sg.median),
                "subject_median": subject_sg.median,
                "baseline_median": baseline_sg.median,
                "concurrency": concurrency,
            }
            if baseline_sg.p75 > 0:
                entry["p75_diff"] = calculate_percentage_diff(subject_sg.p75, baseline_sg.p75)
            if baseline_sg.p95 > 0:
                entry["p95_diff"] = calculate_percentage_diff(subject_sg.p95, baseline_sg.p95)
            items.append(entry)

    return items


def collect_summary_data(grouped: dict, all_grouped: dict, config: dict) -> list[dict]:
    """Collect all summary data from test results."""
    summary_cases = config.get("summary_cases", [])
    default_metrics = config.get("default_summary_metrics", list(config.get("metrics", {}).keys()))
    summary_data = []

    for case in summary_cases:
        test_name = case.get("test", "")
        endpoint = case.get("endpoint", "")
        compare_test = case.get("compare_test", "")
        metric_keys = case.get("metrics", default_metrics)
        list_only = not endpoint and not compare_test

        # Parse test name
        class_short, method_name = (test_name.rsplit(".", 1) + [""])[:2] if "." in test_name else (test_name, "")

        # Find matching class
        search_data = all_grouped if list_only else grouped
        matching_class = next((c for c in search_data if c.endswith(class_short)), None)

        if not matching_class:
            print(f"  Warning: No matching class found for '{test_name}'")
            continue

        methods = search_data[matching_class]
        matching_methods = {k: v for k, v in methods.items() if k.startswith(f"{method_name}(")}

        if not matching_methods:
            print(f"  Warning: No matching methods found for '{test_name}'")
            continue

        # Find comparison methods if needed
        compare_methods = {}
        if compare_test:
            cclass, cmethod = (compare_test.rsplit(".", 1) + [""])[:2] if "." in compare_test else (compare_test, "")
            compare_class = next((c for c in grouped if c.endswith(cclass)), None)
            if compare_class:
                compare_methods = {k: v for k, v in grouped[compare_class].items() if k.startswith(f"{cmethod}(")}

        # Collect results by parallelism
        case_results = {cat: [] for cat in PARALLELISM_ORDER}

        for method_key, scenarios in matching_methods.items():
            concurrency = extract_concurrency(method_key)
            cat_key, _ = get_parallelism_category(concurrency, config)

            if list_only:
                for result in scenarios.values():
                    case_results[cat_key].extend(
                        collect_list_only_metrics(result, metric_keys, config, concurrency)
                    )
            elif endpoint in scenarios:
                subject = scenarios[endpoint]

                if compare_test and compare_methods:
                    # Cross-test comparison
                    compare_mk = next(
                        (k for k in compare_methods if extract_concurrency(k) == concurrency), None
                    )
                    if compare_mk:
                        for baseline in compare_methods[compare_mk].values():
                            case_results[cat_key].extend(
                                collect_comparison_metrics(subject, baseline, metric_keys, config, concurrency)
                            )
                else:
                    # Same-test comparison
                    failed_added = set()
                    for other_label, other in scenarios.items():
                        if other_label == endpoint:
                            continue
                        items = collect_comparison_metrics(subject, other, metric_keys, config, concurrency)
                        # Dedupe failed_count entries
                        for item in items:
                            if item.get("mode") == "failed_count":
                                if item["metric"] in failed_added:
                                    continue
                                failed_added.add(item["metric"])
                            case_results[cat_key].append(item)

        summary_data.append({
            "test": test_name, "endpoint": endpoint, "compare_test": compare_test,
            "metric_keys": metric_keys, "results": case_results, "list_only_mode": list_only,
        })

    return summary_data


# =============================================================================
# Summary Rendering (Shared Logic)
# =============================================================================


@dataclass
class MetricAggregates:
    """Aggregated metric data for rendering."""
    diffs: list[float] = field(default_factory=list)
    p75_diffs: list[float] = field(default_factory=list)
    p95_diffs: list[float] = field(default_factory=list)
    failed: int = 0
    total_requests: int = 0


def aggregate_comparison_metrics(cat_results: list[dict]) -> dict[str, MetricAggregates]:
    """Aggregate comparison metrics by metric key."""
    aggs = defaultdict(MetricAggregates)
    for r in cat_results:
        mk = r["metric"]
        if r.get("mode") == "failed_count":
            aggs[mk].failed += r.get("failed_count", 0)
            aggs[mk].total_requests = r.get("total_requests", 0)
        elif "pct_diff" in r:
            aggs[mk].diffs.append(r["pct_diff"])
            if "p75_diff" in r:
                aggs[mk].p75_diffs.append(r["p75_diff"])
            if "p95_diff" in r:
                aggs[mk].p95_diffs.append(r["p95_diff"])
    return aggs


def aggregate_list_metrics(cat_results: list[dict], config: dict) -> tuple[dict, dict]:
    """Aggregate list-only metrics by scenario. Returns (scenario_metrics, scenario_failed)."""
    scenario_metrics = defaultdict(lambda: defaultdict(list))
    scenario_failed = defaultdict(lambda: defaultdict(lambda: {"failed": 0, "total": 0}))

    for r in cat_results:
        mc = get_metric_config(r["metric"], config)
        scenario = r.get("scenario", "default")
        if mc.get("format") == "failed_count":
            scenario_failed[scenario][r["metric"]]["failed"] += r.get("sum_val", 0)
            scenario_failed[scenario][r["metric"]]["total"] = r.get("total_requests", 0)
        elif r.get("mode") == "list":
            scenario_metrics[scenario][r["metric"]].append({
                "median": r["median"], "p75": r["p75"], "p95": r["p95"], "unit": r["unit"]
            })

    return scenario_metrics, scenario_failed


def render_summary(
    summary_data: list[dict],
    config: dict,
    emit_header: Callable[[str, str, str, bool], None],
    emit_category: Callable[[str], None],
    emit_scenario: Callable[[str], None],
    emit_metric_value: Callable[[str, float, float, float, int], None],
    emit_metric_diff: Callable[[str, float, str, str, str], None],
    emit_failed: Callable[[int, int], None],
    emit_spacing: Callable[[], None],
):
    """
    Generic summary renderer that calls emit functions for output.

    This abstracts the rendering logic so PDF and GitHub can share it.
    """
    parallelism_cfg = config.get("parallelism", {})

    for case_data in summary_data:
        test_name = case_data["test"]
        endpoint = case_data["endpoint"]
        compare_test = case_data.get("compare_test", "")
        metric_keys = case_data.get("metric_keys", [])
        results = case_data["results"]
        list_only = case_data.get("list_only_mode", False)

        # Emit header
        if list_only:
            subtitle = "Metrics by scenario"
        elif compare_test:
            subtitle = f"Endpoint: {endpoint} (vs {compare_test} endpoints)"
        else:
            subtitle = f"Endpoint: {endpoint} (performance vs other endpoints)"

        emit_header(test_name, subtitle, endpoint, list_only)

        # Check for data
        has_data = any(results.get(cat) for cat in PARALLELISM_ORDER)
        if not has_data:
            emit_metric_diff("No data available", 0, "", "", "")
            continue

        for cat_key in PARALLELISM_ORDER:
            cat_label = parallelism_cfg.get(cat_key, {}).get("label", cat_key)
            cat_results = results.get(cat_key, [])
            if not cat_results:
                continue

            emit_category(cat_label)

            if list_only:
                scenario_metrics, scenario_failed = aggregate_list_metrics(cat_results, config)
                all_scenarios = sorted(set(scenario_metrics.keys()) | set(scenario_failed.keys()))

                for scenario in all_scenarios:
                    emit_scenario(scenario)

                    for mk in metric_keys:
                        mc = get_metric_config(mk, config)
                        if mc.get("format") == "failed_count":
                            fd = scenario_failed[scenario].get(mk)
                            if fd and fd["failed"] > 0:
                                emit_failed(int(fd["failed"]), fd["total"])
                            continue

                        values = scenario_metrics[scenario].get(mk)
                        if not values:
                            continue

                        metric_name, _ = get_metric_display_name(mk, config)
                        unit = values[0]["unit"]
                        emit_metric_value(
                            metric_name,
                            avg([v["median"] for v in values]),
                            avg([v["p75"] for v in values]) if mc.get("show_p75") else 0,
                            avg([v["p95"] for v in values]) if mc.get("show_p95") else 0,
                            unit,
                        )
                    emit_spacing()
            else:
                aggs = aggregate_comparison_metrics(cat_results)

                for mk in metric_keys:
                    mc = get_metric_config(mk, config)
                    agg = aggs.get(mk, MetricAggregates())

                    if mc.get("format") == "failed_count":
                        if agg.failed > 0:
                            emit_failed(agg.failed, agg.total_requests)
                        continue

                    if not agg.diffs:
                        continue

                    avg_diff = avg(agg.diffs)
                    metric_name, _ = get_metric_display_name(mk, config)
                    label, color = get_difference_category(avg_diff, config)
                    suffix = format_percentile_diff_suffix(mk, config, avg(agg.p75_diffs) if agg.p75_diffs else None, avg(agg.p95_diffs) if agg.p95_diffs else None)

                    emit_metric_diff(metric_name, avg_diff, label, color, suffix)

            emit_spacing()


# =============================================================================
# PDF Summary Generation
# =============================================================================


def generate_summary_page(
    pdf: PdfPages, grouped: dict, config: dict, all_grouped: Optional[dict] = None
) -> list[dict]:
    """Generate PDF summary page(s)."""
    summary_cases = config.get("summary_cases", [])
    if not summary_cases:
        print("  No summary cases configured, skipping summary page")
        return []

    summary_data = collect_summary_data(grouped, all_grouped or grouped, config)
    if not summary_data:
        return []

    fig = plt.figure(figsize=(11, 8.5))
    fig.suptitle("Performance Summary", fontsize=16, fontweight="bold", y=0.98)
    ax = fig.add_axes([0.05, 0.05, 0.9, 0.85])
    ax.axis("off")

    y_pos = 0.92
    line_height = 0.025

    def new_page_if_needed():
        nonlocal fig, ax, y_pos
        if y_pos < 0.1:
            pdf.savefig(fig, bbox_inches="tight")
            plt.close(fig)
            fig = plt.figure(figsize=(11, 8.5))
            fig.suptitle("Performance Summary (continued)", fontsize=16, fontweight="bold", y=0.98)
            ax = fig.add_axes([0.05, 0.05, 0.9, 0.85])
            ax.axis("off")
            y_pos = 0.92

    def emit_header(test_name, subtitle, endpoint, list_only):
        nonlocal y_pos
        ax.text(0.0, y_pos, test_name, fontsize=11, fontweight="bold", transform=ax.transAxes)
        ax.text(0.0, y_pos - line_height, subtitle, fontsize=9, fontstyle="italic", transform=ax.transAxes, color="#555555")
        y_pos -= line_height * 2.5

    def emit_category(label):
        nonlocal y_pos
        ax.text(0.02, y_pos, f"{label}:", fontsize=10, fontweight="bold", transform=ax.transAxes)
        y_pos -= line_height * 1.2

    def emit_scenario(scenario):
        nonlocal y_pos
        ax.text(0.04, y_pos, f"{scenario}:", fontsize=9, fontweight="bold", transform=ax.transAxes, color="#333333")
        y_pos -= line_height

    def emit_metric_value(name, median, p75, p95, unit):
        nonlocal y_pos
        text = f"    \u2022 {name}: {format_metric_value(median, unit)}"
        if p75:
            text += f" (P75: {format_metric_value(p75, unit)})"
        if p95:
            text += f" (P95: {format_metric_value(p95, unit)})"
        ax.text(0.06, y_pos, text, fontsize=8, transform=ax.transAxes, color="#444444")
        y_pos -= line_height

    def emit_metric_diff(name, diff, label, color, suffix):
        nonlocal y_pos
        if name == "No data available":
            ax.text(0.04, y_pos, "*No data available*", fontsize=9, fontstyle="italic", transform=ax.transAxes, color="#888888")
        else:
            text = f"  \u2022 {name}: {format_pct(diff)} ({label}){suffix}"
            ax.text(0.04, y_pos, text, fontsize=9, transform=ax.transAxes, color=color)
        y_pos -= line_height

    def emit_failed(failed, total):
        nonlocal y_pos
        ax.text(0.04, y_pos, f"  \u2022 Failed: {failed}/{total}", fontsize=9, transform=ax.transAxes, color="#8B0000")
        y_pos -= line_height

    def emit_spacing():
        nonlocal y_pos
        y_pos -= line_height * 0.3
        new_page_if_needed()

    render_summary(
        summary_data, config,
        emit_header, emit_category, emit_scenario, emit_metric_value, emit_metric_diff, emit_failed, emit_spacing
    )

    pdf.savefig(fig, bbox_inches="tight")
    plt.close(fig)
    print("  Generated summary page")
    return summary_data


# =============================================================================
# GitHub Summary Generation
# =============================================================================


def generate_github_summary(summary_data: list[dict], config: dict, output_path: str):
    """Generate markdown summary for GitHub Actions."""
    lines = ["## Performance Summary\n"]

    def emit_header(test_name, subtitle, endpoint, list_only):
        lines.append(f"### {test_name}\n")
        if list_only:
            lines.append("*Metrics by scenario*\n")
        else:
            lines.append(f"*{subtitle}*\n")

    def emit_category(label):
        lines.append(f"**{label}:**\n")

    def emit_scenario(scenario):
        lines.append(f"\n**{scenario}:**\n")

    def emit_metric_value(name, median, p75, p95, unit):
        text = f"- {name}: {format_metric_value(median, unit)}"
        if p75:
            text += f" (P75: {format_metric_value(p75, unit)})"
        if p95:
            text += f" (P95: {format_metric_value(p95, unit)})"
        lines.append(text + "\n")

    def emit_metric_diff(name, diff, label, color, suffix):
        if name == "No data available":
            lines.append("*No data available*\n")
        else:
            emoji = get_diff_emoji(label)
            lines.append(f"- {emoji} **{name}**: {format_pct(diff)} ({label}){suffix}\n")

    def emit_failed(failed, total):
        lines.append(f"- \U0001F534 **Failed**: {failed}/{total}\n")

    def emit_spacing():
        lines.append("\n")

    render_summary(
        summary_data, config,
        emit_header, emit_category, emit_scenario, emit_metric_value, emit_metric_diff, emit_failed, emit_spacing
    )

    # Add separators between test cases
    content = "".join(lines)
    content = re.sub(r"\n(### )", r"\n---\n\1", content)
    content += "---\n"

    with open(output_path, "w", encoding="utf-8") as f:
        f.write(content)

    print(f"  Generated GitHub summary: {output_path}")


# =============================================================================
# Chart Generation
# =============================================================================


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
    """Create bar chart comparing scenarios with percentage annotations."""
    no_data_indices = no_data_indices or []
    colors = list(plt.cm.Set2(np.linspace(0, 1, len(scenarios))))

    for idx in no_data_indices:
        colors[idx] = (0.7, 0.7, 0.7, 1.0)

    x = np.arange(len(scenarios))
    bars = ax.bar(x, values, color=colors, edgecolor="black", linewidth=0.5)

    if show_std_dev and std_devs:
        valid = [(i, v, s) for i, (v, s) in enumerate(zip(values, std_devs)) if i not in no_data_indices]
        if valid:
            ax.errorbar([x[i] for i, _, _ in valid], [v for _, v, _ in valid],
                        yerr=[s for _, _, s in valid], fmt="none", ecolor="black", capsize=3, capthick=1)

    baseline_value = values[baseline_idx] if baseline_idx not in no_data_indices else 0
    max_val = max(values) if values else 1

    for i, (bar, val) in enumerate(zip(bars, values)):
        if i in no_data_indices:
            ax.annotate("No Data", xy=(bar.get_x() + bar.get_width() / 2, max_val * 0.1),
                        ha="center", va="bottom", fontsize=8, color="red", fontweight="bold")
        elif i == baseline_idx:
            ax.annotate("baseline", xy=(bar.get_x() + bar.get_width() / 2, bar.get_height()),
                        xytext=(0, 5), textcoords="offset points", ha="center", va="bottom",
                        fontsize=8, color="blue", fontstyle="italic")
        else:
            pct = calculate_percentage_diff(val, baseline_value)
            ax.annotate(format_pct(pct), xy=(bar.get_x() + bar.get_width() / 2, bar.get_height()),
                        xytext=(0, 5), textcoords="offset points", ha="center", va="bottom",
                        fontsize=8, color="green" if pct <= 0 else "red")

    ax.set_title(title, fontsize=10, fontweight="bold")
    ax.set_xticks(x)
    rotation = 30 if max(len(s) for s in scenarios) > 12 else 0
    ax.set_xticklabels(scenarios, fontsize=8, rotation=rotation, ha="right" if rotation else "center")
    ax.set_ylim(bottom=0)
    ax.grid(axis="y", linestyle="--", alpha=0.7)


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
    """Create grouped bar chart comparing two reports."""
    no_data_indices1 = no_data_indices1 or []
    no_data_indices2 = no_data_indices2 or []

    n = len(scenarios)
    x = np.arange(n)
    width = 0.35

    bars1 = ax.bar(x - width / 2, values1, width, label=report1_label, color="#1f77b4", edgecolor="black", linewidth=0.5)
    bars2 = ax.bar(x + width / 2, values2, width, label=report2_label, color="#ff7f0e", edgecolor="black", linewidth=0.5)

    for idx in no_data_indices1:
        bars1[idx].set_color((0.7, 0.7, 0.7, 1.0))
    for idx in no_data_indices2:
        bars2[idx].set_color((0.7, 0.7, 0.7, 1.0))

    def add_errorbars(x_offset, values, stds, no_data):
        if show_std_dev and stds:
            valid = [(i, v, s) for i, (v, s) in enumerate(zip(values, stds)) if i not in no_data]
            if valid:
                ax.errorbar([x[i] + x_offset for i, _, _ in valid], [v for _, v, _ in valid],
                            yerr=[s for _, _, s in valid], fmt="none", ecolor="black", capsize=2, capthick=1)

    add_errorbars(-width / 2, values1, std_devs1, no_data_indices1)
    add_errorbars(width / 2, values2, std_devs2, no_data_indices2)

    baseline_value = values1[baseline_idx] if baseline_idx not in no_data_indices1 else 0
    max_val = max(max(values1, default=1), max(values2, default=1))

    for i, (bar, val) in enumerate(zip(bars1, values1)):
        if i in no_data_indices1:
            ax.annotate("N/A", xy=(bar.get_x() + bar.get_width() / 2, max_val * 0.05),
                        ha="center", va="bottom", fontsize=7, color="red")
        elif i == baseline_idx:
            ax.annotate("base", xy=(bar.get_x() + bar.get_width() / 2, bar.get_height()),
                        xytext=(0, 3), textcoords="offset points", ha="center", va="bottom",
                        fontsize=7, color="blue", fontstyle="italic")

    for i, (bar, val) in enumerate(zip(bars2, values2)):
        if i in no_data_indices2:
            ax.annotate("N/A", xy=(bar.get_x() + bar.get_width() / 2, max_val * 0.05),
                        ha="center", va="bottom", fontsize=7, color="red")
        elif baseline_value > 0:
            pct = calculate_percentage_diff(val, baseline_value)
            ax.annotate(format_pct(pct), xy=(bar.get_x() + bar.get_width() / 2, bar.get_height()),
                        xytext=(0, 3), textcoords="offset points", ha="center", va="bottom",
                        fontsize=7, color="green" if pct <= 0 else "red")

    ax.set_title(title, fontsize=10, fontweight="bold")
    ax.set_xticks(x)
    rotation = 30 if max(len(s) for s in scenarios) > 10 else 0
    ax.set_xticklabels(scenarios, fontsize=7 if rotation else 8, rotation=rotation, ha="right" if rotation else "center")
    ax.set_ylim(bottom=0)
    ax.grid(axis="y", linestyle="--", alpha=0.7)
    ax.legend(loc="upper right", fontsize=8)


# =============================================================================
# Report Generation
# =============================================================================


def generate_comparison_report(
    json_path1: str,
    json_path2: str,
    output_path: str,
    fixture_filter: str,
    config_path: Optional[Path] = None,
    report1_label: str = "Report 1",
    report2_label: str = "Report 2",
):
    """Generate PDF comparing two performance reports for a specific test fixture."""
    config = load_config(config_path) if config_path else load_config()

    print(f"Comparing reports for fixture: {fixture_filter}")
    print(f"  Report 1: {json_path1}")
    print(f"  Report 2: {json_path2}")

    data1, data2 = load_performance_results(json_path1), load_performance_results(json_path2)
    grouped1, grouped2 = group_test_results(data1), group_test_results(data2)

    matching1 = next((c for c in grouped1 if fixture_filter in c), None)
    matching2 = next((c for c in grouped2 if fixture_filter in c), None)

    if not matching1:
        print(f"Error: No matching class found for '{fixture_filter}' in Report 1")
        return
    if not matching2:
        print(f"Error: No matching class found for '{fixture_filter}' in Report 2")
        return

    methods1, methods2 = grouped1[matching1], grouped2[matching2]
    all_methods = sorted(set(methods1.keys()) | set(methods2.keys()))

    print(f"Found {len(all_methods)} methods to compare")

    with PdfPages(output_path) as pdf:
        for method_key in all_methods:
            scenarios1, scenarios2 = methods1.get(method_key, {}), methods2.get(method_key, {})
            all_scenarios = sorted(set(scenarios1.keys()) | set(scenarios2.keys()))

            if not all_scenarios:
                continue

            baseline_label = determine_baseline(scenarios1) if scenarios1 else determine_baseline(scenarios2) if scenarios2 else all_scenarios[0]
            if baseline_label not in all_scenarios:
                baseline_label = all_scenarios[0]
            baseline_idx = all_scenarios.index(baseline_label)

            time_groups = sorted({
                sg for result in list(scenarios1.values()) + list(scenarios2.values())
                for sg, data in result.sample_groups.items() if data.is_time_based()
            })

            if not time_groups:
                continue

            for sg_name in time_groups:
                def collect_data(scenarios, no_data_list):
                    medians, stds, p95s, p99s = [], [], [], []
                    for idx, label in enumerate(all_scenarios):
                        result = scenarios.get(label)
                        sg = result.sample_groups.get(sg_name) if result else None
                        if sg:
                            medians.append(sg.median)
                            stds.append(sg.std_dev)
                            p95s.append(sg.p95)
                            p99s.append(sg.p99)
                        else:
                            no_data_list.append(idx)
                            medians.append(0)
                            stds.append(0)
                            p95s.append(0)
                            p99s.append(0)
                    return medians, stds, p95s, p99s

                no_data1, no_data2 = [], []
                medians1, stds1, p95s1, p99s1 = collect_data(scenarios1, no_data1)
                medians2, stds2, p95s2, p99s2 = collect_data(scenarios2, no_data2)

                if len(no_data1) == len(all_scenarios) and len(no_data2) == len(all_scenarios):
                    continue

                fig, axes = plt.subplots(1, 3, figsize=(15, 5))
                sample = next(iter(scenarios1.values()), None) or next(iter(scenarios2.values()), None)
                fig.suptitle(
                    f"{matching1.split('.')[-1]}.{sample.method_name if sample else method_key} - {sg_name}\n"
                    f"Params: {sample.test_case_args if sample else ''}",
                    fontsize=11, fontweight="bold"
                )

                for ax, (title, v1, v2, s1, s2) in zip(axes, [
                    ("Median", medians1, medians2, stds1, stds2),
                    ("P95", p95s1, p95s2, [s * 0.8 for s in stds1], [s * 0.8 for s in stds2]),
                    ("P99", p99s1, p99s2, [s * 0.9 for s in stds1], [s * 0.9 for s in stds2]),
                ]):
                    create_comparison_chart_dual_report(
                        ax, title, all_scenarios, v1, v2, baseline_idx,
                        std_devs1=s1, std_devs2=s2, show_std_dev=True,
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
    """Generate PDF report from performance test results."""
    config = load_config(config_path) if config_path else load_config()

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

    # Filter to multi-scenario classes
    filtered = {}
    for cls, methods in grouped.items():
        comparable = {k: v for k, v in methods.items() if len(v) >= 2}
        if comparable:
            filtered[cls] = comparable
        else:
            print(f"  Skipping class '{cls}': only one TestFixture scenario")

    if not filtered and not grouped:
        print("No test results found!")
        return

    print(f"Processing {len(filtered)} test classes with multiple scenarios")

    with PdfPages(output_path) as pdf:
        print("Generating summary page...")
        summary_data = generate_summary_page(pdf, filtered, config, all_grouped=grouped)

        if github_summary_path and summary_data:
            generate_github_summary(summary_data, config, github_summary_path)

        if summary_only:
            print(f"\nSummary-only report saved to: {output_path}")
            return

        # Detailed charts
        for class_name, methods in filtered.items():
            print(f"Processing class: {class_name}")

            for method_key, scenarios in methods.items():
                labels = list(scenarios.keys())
                baseline = determine_baseline(scenarios)
                baseline_idx = labels.index(baseline)

                time_groups = sorted({
                    sg for r in scenarios.values() for sg, d in r.sample_groups.items() if d.is_time_based()
                })

                if not time_groups:
                    continue

                for sg_name in time_groups:
                    medians, stds, p95s, p99s, no_data = [], [], [], [], []

                    for idx, label in enumerate(labels):
                        sg = scenarios[label].sample_groups.get(sg_name)
                        if sg:
                            medians.append(sg.median)
                            stds.append(sg.std_dev)
                            p95s.append(sg.p95)
                            p99s.append(sg.p99)
                        else:
                            no_data.append(idx)
                            medians.append(0)
                            stds.append(0)
                            p95s.append(0)
                            p99s.append(0)

                    if len(no_data) == len(labels):
                        continue

                    fig, axes = plt.subplots(1, 3, figsize=(14, 4))
                    short_class = class_name.split(".")[-1]
                    fig.suptitle(
                        f"{short_class}.{scenarios[labels[0]].method_name} - {sg_name}\n"
                        f"Params {scenarios[labels[0]].test_case_args}",
                        fontsize=11, fontweight="bold"
                    )

                    p95_stds = [s * 0.8 if scenarios[labels[i]].sample_groups.get(sg_name, None) and scenarios[labels[i]].sample_groups[sg_name].samples else 0 for i, s in enumerate(stds)]
                    p99_stds = [s * 0.9 if scenarios[labels[i]].sample_groups.get(sg_name, None) and scenarios[labels[i]].sample_groups[sg_name].samples else 0 for i, s in enumerate(stds)]

                    for ax, (title, vals, devs) in zip(axes, [
                        ("Median", medians, stds),
                        ("P95", p95s, p95_stds),
                        ("P99", p99s, p99_stds),
                    ]):
                        create_comparison_chart(ax, title, labels, vals, baseline_idx, std_devs=devs, no_data_indices=no_data)

                    plt.tight_layout()
                    pdf.savefig(fig, bbox_inches="tight")
                    plt.close(fig)

                # Downloaded data chart
                downloaded, download_no_data = [], []
                for idx, label in enumerate(labels):
                    sg = scenarios[label].sample_groups.get("Iteration Downloaded Data")
                    if sg:
                        downloaded.append(sg.max_val)
                    else:
                        download_no_data.append(idx)
                        downloaded.append(0)

                if any(d > 0 for d in downloaded):
                    fig, ax = plt.subplots(1, 1, figsize=(6, 4))
                    short_class = class_name.split(".")[-1]
                    fig.suptitle(
                        f"{short_class}.{scenarios[labels[0]].method_name} - Downloaded Data\n"
                        f"Params {scenarios[labels[0]].test_case_args}",
                        fontsize=11, fontweight="bold"
                    )
                    create_comparison_chart(ax, "Downloaded (Max MB)", labels, downloaded, baseline_idx, show_std_dev=False, no_data_indices=download_no_data)
                    plt.tight_layout()
                    pdf.savefig(fig, bbox_inches="tight")
                    plt.close(fig)

                print(f"  Generated charts for: {method_key} (baseline: {baseline}, {len(time_groups)} metrics)")

    print(f"\nReport saved to: {output_path}")


# =============================================================================
# CLI
# =============================================================================


def parse_args() -> argparse.Namespace:
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(
        description="Generate performance benchmark reports from Unity test results.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__
    )

    # Single report mode
    parser.add_argument("input_json", nargs="?", help="Input JSON file from Unity Performance Test Framework")
    parser.add_argument("output_pdf", nargs="?", default="PerformanceBenchmarkReport.pdf", help="Output PDF file")
    parser.add_argument("--summary-only", action="store_true", help="Generate only summary page")
    parser.add_argument("--github-summary", metavar="PATH", help="Output markdown summary for GitHub Actions")

    # Compare mode
    parser.add_argument("--compare", action="store_true", help="Compare two reports (requires --fixture)")
    parser.add_argument("--fixture", metavar="NAME", help="TestFixture name to compare (required for --compare)")
    parser.add_argument("--label1", default="Report 1", help="Label for first report")
    parser.add_argument("--label2", default="Report 2", help="Label for second report")

    return parser.parse_args()


def main():
    args = parse_args()

    if args.compare:
        # Compare mode: input_json and output_pdf become report1 and report2
        if not args.fixture:
            print("Error: --fixture is required for compare mode")
            sys.exit(1)

        if not args.input_json or not args.output_pdf:
            print("Error: Compare mode requires <report1.json> <report2.json> <output.pdf>")
            print("Usage: python generate_perf_report.py --compare <report1> <report2> <output> --fixture <name>")
            sys.exit(1)

        # In compare mode, we need 3 positional args but argparse only captures 2
        # Check if there's an extra arg in sys.argv
        positional = [a for a in sys.argv[1:] if not a.startswith("-") and a not in [args.fixture, args.label1, args.label2]]
        if len(positional) < 3:
            print("Error: Compare mode requires <report1.json> <report2.json> <output.pdf>")
            sys.exit(1)

        report1, report2, output = positional[0], positional[1], positional[2]

        if not Path(report1).exists():
            print(f"Error: Report 1 not found: {report1}")
            sys.exit(1)
        if not Path(report2).exists():
            print(f"Error: Report 2 not found: {report2}")
            sys.exit(1)

        generate_comparison_report(
            report1, report2, output, args.fixture,
            report1_label=args.label1, report2_label=args.label2
        )
    else:
        # Single report mode
        if not args.input_json:
            print("Error: Input JSON file required")
            print("Usage: python generate_perf_report.py <input_json> [output_pdf]")
            sys.exit(1)

        if not Path(args.input_json).exists():
            print(f"Error: Input file not found: {args.input_json}")
            sys.exit(1)

        generate_report(
            args.input_json, args.output_pdf,
            summary_only=args.summary_only,
            github_summary_path=args.github_summary
        )


if __name__ == "__main__":
    main()
