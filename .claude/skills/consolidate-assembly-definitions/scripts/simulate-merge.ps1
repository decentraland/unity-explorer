param(
    [Parameter(Mandatory)][string]$Anchor,
    [Parameter(Mandatory)][string[]]$Members,
    [string]$GraphPath = "$env:TEMP\asm-analysis\graph.json"
)
# Simulates folding $Members into $Anchor: the merged node's union of outward
# references must not transitively reach any group member. Prints OK or the cycle chain.
# Also runs a full-graph DFS afterwards to confirm the current graph itself is cycle-free.
$g = Get-Content $GraphPath -Raw | ConvertFrom-Json
$names = @{}; foreach ($a in $g) { $names[$a.name] = $a }
$adj = @{}; foreach ($a in $g) { $adj[$a.name] = @($a.references | Where-Object { $names.ContainsKey($_) }) }

$group = @($Anchor) + $Members
foreach ($x in $group) { if (-not $adj.ContainsKey($x)) { Write-Output "MISSING assembly: $x"; exit 1 } }
$union = @(); foreach ($x in $group) { $union += $adj[$x] }
$union = $union | Sort-Object -Unique | Where-Object { $group -notcontains $_ }
$result = "OK"
foreach ($start in $union) {
    $visited = New-Object System.Collections.Generic.HashSet[string]
    $queue = New-Object System.Collections.Generic.Queue[string]
    $parent = @{}
    $queue.Enqueue($start); $null = $visited.Add($start)
    while ($queue.Count -gt 0) {
        $cur = $queue.Dequeue()
        foreach ($n in $adj[$cur]) {
            if ($group -contains $n) {
                $chain = @($n, $cur); $p = $cur
                while ($parent.ContainsKey($p)) { $p = $parent[$p]; $chain += $p }
                $result = "CYCLE: " + (($chain[($chain.Count-1)..0]) -join " -> ")
                break
            }
            if (-not $visited.Contains($n)) { $null = $visited.Add($n); $parent[$n] = $cur; $queue.Enqueue($n) }
        }
        if ($result -ne "OK") { break }
    }
    if ($result -ne "OK") { break }
}
"merge [$($Members -join ', ')] -> ${Anchor}: $result"

# full-graph cycle check (sanity on current state)
$color = @{}; foreach ($k in $adj.Keys) { $color[$k] = 0 }; $cycle = $null
function Visit($n) {
    $script:color[$n] = 1
    foreach ($m in $adj[$n]) {
        if ($script:color[$m] -eq 1) { $script:cycle = "$n -> $m"; return $true }
        if ($script:color[$m] -eq 0) { if (Visit $m) { return $true } }
    }
    $script:color[$n] = 2; $false
}
foreach ($k in @($adj.Keys)) { if ($color[$k] -eq 0) { if (Visit $k) { break } } }
if ($cycle) { "current graph CYCLE: $cycle" } else { "current graph: cycle-free" }
