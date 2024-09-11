param(
    [Parameter(Mandatory=$true)]
    [string]$InputLog,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputReport
)

function ConvertToReadableTime($seconds) {
    $time = [TimeSpan]::FromSeconds($seconds)
    return "{0:D2}:{1:D2}:{2:D2}" -f $time.Hours, $time.Minutes, $time.Seconds
}

function FormatTableRow($columns, $widths) {
    $row = "|"
    for ($i = 0; $i -lt $columns.Count; $i++) {
        $value = if ($columns[$i] -is [DateTime]) { $columns[$i].ToString("yyyy-MM-dd HH:mm:ss") } else { $columns[$i].ToString() }
        $width = if ($widths -and $i -lt $widths.Count) { $widths[$i] } else { $value.Length }
        $row += " {0,-$width} |" -f ($value.Substring(0, [Math]::Min($value.Length, $width)).PadRight($width))
    }
    return $row
}

$shaderData = @()
$currentShader = $null
$totalVariants = 0
$localCacheHits = 0
$remoteCacheHits = 0

Get-Content $InputLog | ForEach-Object {
    if ($_ -match '\[(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z).+Compiling shader "(.+)" pass "(.+)" \((.+)\)') {
        if ($currentShader) {
            $shaderData += $currentShader
        }
        $currentShader = [PSCustomObject]@{
            StartTime = [DateTime]::ParseExact($Matches[1], "yyyy-MM-ddTHH:mm:ssZ", $null)
            Name = $Matches[2]
            Pass = $Matches[3]
            Type = $Matches[4]
            Duration = $null
            Variants = 0
        }
    }
    elseif ($_ -match 'After scriptable stripping: (\d+)' -and $currentShader) {
        $currentShader.Variants = [int]$Matches[1]
        $totalVariants += $currentShader.Variants
    }
    elseif ($_ -match 'finished in ([\d.]+) seconds' -and $currentShader) {
        $currentShader.Duration = [double]$Matches[1]
    }
    elseif ($_ -match 'Local cache hits (\d+)') {
        $localCacheHits += [int]$Matches[1]
    }
    elseif ($_ -match 'remote cache hits (\d+)') {
        $remoteCacheHits += [int]$Matches[1]
    }
}

if ($currentShader) {
    $shaderData += $currentShader
}

$reportContent = "Unity Shader Compilation Report`n"
$reportContent += "================================`n`n"

$totalDuration = 0

foreach ($shader in $shaderData) {
    $readableTime = ConvertToReadableTime($shader.Duration)
    $reportContent += "Shader: $($shader.Name)`n"
    $reportContent += "Pass: $($shader.Pass)`n"
    $reportContent += "Type: $($shader.Type)`n"
    $reportContent += "Start Time: $($shader.StartTime)`n"
    $reportContent += "Duration: $readableTime`n"
    $reportContent += "Variants: $($shader.Variants)`n"
    $reportContent += "------------------`n"
    $totalDuration += $shader.Duration
}

$totalReadableTime = ConvertToReadableTime($totalDuration)
$reportContent += "`nTotal Compilation Time: $totalReadableTime`n`n"

# Summary Section
$summaryContent = "Summary:`n"
$summaryContent += "--------`n"
$totalShaders = $shaderData.Count
$averageDuration = $totalDuration / $totalShaders
$top5Slowest = $shaderData | Sort-Object Duration -Descending | Select-Object -First 5

$summaryContent += "Total Shaders Compiled: $totalShaders`n"
$summaryContent += "Total Variants Compiled: $totalVariants`n"
$summaryContent += "Total Compilation Time: $(ConvertToReadableTime($totalDuration))`n"
$summaryContent += "Average Compilation Time: $(ConvertToReadableTime($averageDuration))`n"
$summaryContent += "Local Cache Hits: $localCacheHits`n"
$summaryContent += "Remote Cache Hits: $remoteCacheHits`n"

# Shader types summary
$shaderTypes = $shaderData | Group-Object Type | Sort-Object Count -Descending
$summaryContent += "`nShader Types:`n"
foreach ($type in $shaderTypes) {
    $summaryContent += "  $($type.Name): $($type.Count) shaders`n"
}

$summaryContent += "`nTop 5 Slowest Compilations:`n"
foreach ($shader in $top5Slowest) {
    $summaryContent += "  Shader: $($shader.Name)`n"
    $summaryContent += "  Pass: $($shader.Pass)`n"
    $summaryContent += "  Type: $($shader.Type)`n"
    $summaryContent += "  Duration: $(ConvertToReadableTime($shader.Duration))`n"
    $summaryContent += "  Variants: $($shader.Variants)`n"
    $summaryContent += "  ---`n"
}

$tableContent = "`n"

$columnWidths = @(30, 20, 10, 19, 10, 10)
$headerRow = FormatTableRow @("Shader", "Pass", "Type", "Start Time", "Duration", "Variants") $columnWidths
$tableContent += $headerRow + "`n"
$tableContent += "-" * ($headerRow.Length - 1) + "`n"

foreach ($shader in $shaderData) {
    $readableTime = ConvertToReadableTime($shader.Duration)
    $row = FormatTableRow @($shader.Name, $shader.Pass, $shader.Type, $shader.StartTime, $readableTime, $shader.Variants) $columnWidths
    $tableContent += $row + "`n"
}

# Combine all content for the full report
$reportContent += $summaryContent + $tableContent

# Write full report to file
$reportContent | Out-File -FilePath $OutputReport

# Output summary and table to console
Write-Host $summaryContent
Write-Host $tableContent

Write-Host "`nFull report generated and saved to $OutputReport"