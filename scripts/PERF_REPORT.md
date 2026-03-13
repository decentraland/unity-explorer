# Performance Benchmark Report Generator

A Python script that generates PDF reports comparing Unity Performance Test Framework results across different test scenarios.

## Location

```
unity-explorer/
├── scripts/
│   ├── generate_perf_report.py      # Main script
│   ├── perf_report_config.json      # Configuration file
│   ├── requirements-perf-report.txt # Python dependencies
│   └── PERF_REPORT.md               # This documentation
└── Explorer/
    └── PerformanceTestResults.json  # Test output (generated)
```

## Requirements

### Windows

```cmd
py -m pip install -r scripts\requirements-perf-report.txt
```

Or with Python launcher:
```cmd
python -m pip install -r scripts\requirements-perf-report.txt
```

### macOS / Linux

```bash
pip3 install -r scripts/requirements-perf-report.txt
```

Or with virtual environment (recommended):
```bash
python3 -m venv venv
source venv/bin/activate
pip install -r scripts/requirements-perf-report.txt
```

### CI (GitHub Actions)

```yaml
- name: Set up Python
  uses: actions/setup-python@v5
  with:
    python-version: '3.11'

- name: Install dependencies
  run: pip install -r scripts/requirements-perf-report.txt
```

## Usage

### Standard Report

Generate a full PDF report with summary page and detailed charts:

```bash
python generate_perf_report.py <input.json> [output.pdf]
```

**Example:**
```bash
python generate_perf_report.py PerformanceTestResults.json report.pdf
```

### Summary Only

Generate only the summary page:

```bash
python generate_perf_report.py <input.json> <output.pdf> --summary-only
```

### GitHub Actions Summary

Output markdown summary for `$GITHUB_STEP_SUMMARY`:

```bash
python generate_perf_report.py <input.json> <output.pdf> --github-summary summary.md
```

**In CI workflow:**
```yaml
- name: Generate performance report
  run: |
    python scripts/generate_perf_report.py results.json report.pdf --github-summary summary.md
    cat summary.md >> $GITHUB_STEP_SUMMARY
```

### Compare Two Reports

Compare results from two test runs side-by-side:

```bash
python generate_perf_report.py --compare <report1.json> <report2.json> <output.pdf> --fixture <TestFixtureName>
```

**Options:**
- `--fixture <name>` - (Required) Filter to a specific test fixture class
- `--label1 <name>` - Label for first report (default: "Report 1")
- `--label2 <name>` - Label for second report (default: "Report 2")

**Example:**
```bash
python generate_perf_report.py --compare baseline.json new.json comparison.pdf \
  --fixture ProfilesPerformanceTest \
  --label1 "Before" \
  --label2 "After"
```

## Configuration

The script uses `perf_report_config.json` in the same directory.

### Configuration Structure

```json
{
  "difference_thresholds": [...],
  "metrics": {...},
  "default_summary_metrics": [...],
  "parallelism": {...},
  "summary_cases": [...]
}
```

### Difference Thresholds

Define how percentage differences are categorized and colored:

```json
"difference_thresholds": [
  {
    "min": null,
    "max": -30,
    "label": "Major Improvement",
    "color": "#006400"
  },
  {
    "min": -10,
    "max": 10,
    "label": "Within Margin of Error",
    "color": "#808080"
  },
  {
    "min": 30,
    "max": null,
    "label": "Major Regression",
    "color": "#8B0000"
  }
]
```

- `min`/`max`: Percentage bounds (use `null` for unbounded)
- `label`: Human-readable category name
- `color`: Hex color for PDF output

### Metrics

Define human-readable names for sample groups:

```json
"metrics": {
  "WebRequest.Send": {
    "name": "Web Request Send Time",
    "description": "time to send the web request"
  },
  "Iteration Total Time": {
    "name": "Total Iteration Time",
    "description": "total time for the complete operation"
  }
}
```

### Default Summary Metrics

Specify which metrics appear in summary by default:

```json
"default_summary_metrics": [
  "WebRequest.Send",
  "WebRequest.ProcessData",
  "Iteration Total Time"
]
```

### Parallelism Categories

Group results by concurrency level:

```json
"parallelism": {
  "no_concurrency": {
    "min": 1,
    "max": 1,
    "label": "No Concurrency"
  },
  "low_concurrency": {
    "min": 2,
    "max": 10,
    "label": "Low Concurrency"
  },
  "high_concurrency": {
    "min": 11,
    "max": null,
    "label": "High Concurrency"
  }
}
```

The script extracts concurrency from the first test case argument.

### Summary Cases

Configure which tests appear on the summary page and how they're compared.

#### Mode 1: Endpoint Comparison (within same test)

Compare one endpoint against others in the same test:

```json
{
  "test": "ProfilesPerformanceTest.PostProfilesAsync",
  "endpoint": "asset-bundle-registry"
}
```

Output shows percentage difference of `asset-bundle-registry` vs other endpoints.

#### Mode 2: Cross-Test Comparison

Compare an endpoint from one test against endpoints from another test:

```json
{
  "test": "ProfilesPerformanceTest.PostMetadataAsync",
  "endpoint": "asset-bundle-registry",
  "compare_test": "ProfilesPerformanceTest.PostProfilesAsync"
}
```

Output shows how `PostMetadataAsync.asset-bundle-registry` performs vs all `PostProfilesAsync` endpoints.

#### Mode 3: List Only (no comparison)

List raw metric values without comparison (when `endpoint` is omitted):

```json
{
  "test": "RPCFriendsServiceBenchmark.GetFriendsAsync"
}
```

Output shows Median and P95 values for each scenario grouped by parallelism.

#### Per-Case Metrics

Override default metrics for specific cases:

```json
{
  "test": "ProfilesPerformanceTest.PostMetadataAsync",
  "endpoint": "asset-bundle-registry",
  "compare_test": "ProfilesPerformanceTest.PostProfilesAsync",
  "metrics": [
    "WebRequest.Send",
    "WebRequest.ProcessData",
    "Iteration Total Time",
    "Iteration Downloaded Data"
  ]
}
```

## Input Format

The script expects Unity Performance Test Framework JSON output:

```json
{
  "Results": [
    {
      "Name": "Namespace.TestClass(fixtureArg1,fixtureArg2).MethodName(testArg1,testArg2)",
      "SampleGroups": [
        {
          "Name": "WebRequest.Send",
          "Unit": 1,
          "Median": 12345.67,
          "Min": 1000.0,
          "Max": 50000.0,
          "StandardDeviation": 5000.0,
          "Samples": [1000, 2000, 3000, ...]
        }
      ]
    }
  ]
}
```

### Unit Values

| Value | Unit |
|-------|------|
| 0 | Undefined |
| 1 | Microsecond |
| 2 | Millisecond |
| 3 | Second |
| 4 | Byte |
| 5 | Kilobyte |
| 6 | Megabyte |
| 7 | Gigabyte |
| 8 | Nanosecond |

### Baseline Detection

The script determines baseline from TestFixture arguments:
- Extracts the second parameter from fixture args
- If `True`, that scenario is the baseline
- Example: `TestClass("https://endpoint.com",True)` marks this as baseline

### Scenario Labels

Labels are extracted from TestFixture URL arguments:
- `https://peer-ap1.decentraland.org/...` -> `peer-ap1`
- `https://asset-bundle-registry.decentraland.today/...` -> `asset-bundle-registry`
- `https://gateway.decentraland.zone/...` -> `gateway.zone` (includes TLD for distinction)

## Output

### PDF Report

- **Summary Page**: Overview of configured test cases with percentage comparisons or raw metrics
- **Detail Pages**: Per-method charts showing Median, P95, P99 with error bars
- Baseline marked with "baseline" label
- Percentage annotations colored by threshold category

### GitHub Summary (Markdown)

The GitHub summary uses emoji indicators:
- 🟢 Green: Improvement
- 🔴 Red: Regression
- ⚪ White: Within margin of error

## Output Examples

### Endpoint Comparison Mode

Config:
```json
{
  "test": "ProfilesPerformanceTest.PostProfilesAsync",
  "endpoint": "asset-bundle-registry"
}
```

Output:
```markdown
### ProfilesPerformanceTest.PostProfilesAsync
*Endpoint: `asset-bundle-registry` (performance vs other endpoints)*
**No Concurrency:**
- 🟢 **Web Request Send Time**: -65.1% (Major Improvement)
- 🟢 **Data Processing Time**: -15.7% (Slight Improvement)
- 🟢 **Total Iteration Time**: -66.7% (Major Improvement)

**Low Concurrency:**
- 🟢 **Web Request Send Time**: -52.6% (Major Improvement)
- 🔴 **Data Processing Time**: +144.7% (Major Regression)
- 🟢 **Total Iteration Time**: -53.3% (Major Improvement)

**High Concurrency:**
- 🟢 **Web Request Send Time**: -66.9% (Major Improvement)
- 🔴 **Data Processing Time**: +307.3% (Major Regression)
- 🟢 **Total Iteration Time**: -56.0% (Major Improvement)
```

### Cross-Test Comparison Mode

Config:
```json
{
  "test": "ProfilesPerformanceTest.PostMetadataAsync",
  "endpoint": "asset-bundle-registry",
  "compare_test": "ProfilesPerformanceTest.PostProfilesAsync",
  "metrics": [
    "WebRequest.Send",
    "WebRequest.ProcessData",
    "Iteration Total Time",
    "Iteration Downloaded Data"
  ]
}
```

Output:
```markdown
### ProfilesPerformanceTest.PostMetadataAsync
*Endpoint: `asset-bundle-registry` vs `ProfilesPerformanceTest.PostProfilesAsync` endpoints*
**No Concurrency:**
- 🟢 **Web Request Send Time**: -53.3% (Major Improvement)
- 🟢 **Data Processing Time**: -91.2% (Major Improvement)
- 🟢 **Total Iteration Time**: -55.2% (Major Improvement)
- 🟢 **Downloaded Data**: -92.2% (Major Improvement)

**Low Concurrency:**
- 🟢 **Web Request Send Time**: -63.4% (Major Improvement)
- 🟢 **Data Processing Time**: -93.3% (Major Improvement)
- 🟢 **Total Iteration Time**: -66.2% (Major Improvement)
- 🟢 **Downloaded Data**: -92.2% (Major Improvement)
```

### List-Only Mode (No Comparison)

Config:
```json
{
  "test": "ProfilesPerformanceTest.PostProfilesAsync"
}
```

Output:
```markdown
### ProfilesPerformanceTest.PostProfilesAsync
*Metrics by scenario*
**No Concurrency:**

**asset-bundle-registry:**
- Web Request Send Time: 51.1ms (P95: 71.3ms)
- Data Processing Time: 18.1ms (P95: 32.5ms)
- Total Iteration Time: 5554.4ms (P95: 5771.7ms)

**peer-ap1:**
- Web Request Send Time: 284.5ms (P95: 770.9ms)
- Data Processing Time: 21.5ms (P95: 34.2ms)
- Total Iteration Time: 33759.2ms (P95: 34983.2ms)

**peer-ec1:**
- Web Request Send Time: 80.2ms (P95: 162.7ms)
- Data Processing Time: 21.6ms (P95: 36.2ms)
- Total Iteration Time: 9017.6ms (P95: 10293.9ms)

**Low Concurrency:**

**asset-bundle-registry:**
- Web Request Send Time: 86.0ms (P95: 132.9ms)
- Data Processing Time: 57.7ms (P95: 124.6ms)
- Total Iteration Time: 1014.2ms (P95: 1362.6ms)

**peer-ap1:**
- Web Request Send Time: 299.6ms (P95: 767.2ms)
- Data Processing Time: 21.8ms (P95: 39.1ms)
- Total Iteration Time: 3791.6ms (P95: 4116.4ms)
```

### Within Margin of Error

Config:
```json
{
  "test": "AssetBundleRegistryPerformanceTests.GetEntitiesActive",
  "endpoint": "gateway.zone"
}
```

Output:
```markdown
### AssetBundleRegistryPerformanceTests.GetEntitiesActive
*Endpoint: `gateway.zone` (performance vs other endpoints)*
**Low Concurrency:**
- ⚪ **Web Request Send Time**: +10.0% (Within Margin of Error)
- ⚪ **Data Processing Time**: +2.2% (Within Margin of Error)
- 🔴 **Total Iteration Time**: +12.3% (Slight Regression)

**High Concurrency:**
- ⚪ **Web Request Send Time**: +9.5% (Within Margin of Error)
- ⚪ **Data Processing Time**: -1.6% (Within Margin of Error)
- ⚪ **Total Iteration Time**: +9.7% (Within Margin of Error)
```

### Dual Report Comparison (CLI)

Command:
```bash
python generate_perf_report.py --compare baseline.json new.json comparison.pdf \
  --fixture ProfilesPerformanceTest --label1 "Before" --label2 "After"
```

PDF Output (per chart):
- Grouped bar chart with two bars per scenario
- Blue bars: "Before" (Report 1)
- Orange bars: "After" (Report 2)
- Baseline scenario marked with "base" label on Report 1 bar
- Report 2 bars show percentage difference vs Report 1 baseline
- Charts for Median, P95, P99 side by side

## Examples

### Full CI Pipeline

```yaml
- name: Generate performance report
  run: |
    python scripts/generate_perf_report.py \
      Explorer/PerformanceTestResults.json \
      Explorer/PerformanceBenchmarkReport.pdf \
      --github-summary Explorer/summary.md
    cat Explorer/summary.md >> $GITHUB_STEP_SUMMARY

- name: Upload report
  uses: actions/upload-artifact@v4
  with:
    name: Performance Report
    path: Explorer/PerformanceBenchmarkReport.pdf
```

### Compare Before/After

```bash
# Run baseline tests
python generate_perf_report.py --compare \
  results_main.json \
  results_feature.json \
  comparison.pdf \
  --fixture ProfilesPerformanceTest \
  --label1 "main" \
  --label2 "feature-branch"
```
