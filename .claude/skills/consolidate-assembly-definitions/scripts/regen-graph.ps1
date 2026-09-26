param(
    [string]$Suffix = "",
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\..\..\..").Path,
    [string]$OutDir = "$env:TEMP\asm-analysis"
)
$root = Join-Path $ProjectRoot "Explorer"
New-Item -ItemType Directory -Force $OutDir | Out-Null
$all = Get-ChildItem -Recurse -Filter *.asmdef -Path $root -ErrorAction SilentlyContinue
$guidMap = @{}
foreach ($f in $all) {
    $meta = "$($f.FullName).meta"
    if (Test-Path $meta) {
        $guidLine = (Select-String -Path $meta -Pattern '^guid:\s*(\w+)' | Select-Object -First 1)
        if ($guidLine) {
            $guid = $guidLine.Matches[0].Groups[1].Value
            try { $name = (Get-Content $f.FullName -Raw | ConvertFrom-Json).name } catch { $name = $f.BaseName }
            $guidMap[$guid] = $name
        }
    }
}
$guidMap.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" } | Set-Content "$OutDir\guidmap$Suffix.txt" -Encoding utf8
$assets = $all | Where-Object { $_.FullName -like "$root\Assets\*" }
$graph = foreach ($f in $assets) {
    $j = Get-Content $f.FullName -Raw | ConvertFrom-Json
    $refs = @()
    if ($j.references) {
        foreach ($r in $j.references) {
            if ($r -like "GUID:*") {
                $g = $r.Substring(5)
                if ($guidMap.ContainsKey($g)) { $refs += $guidMap[$g] } else { $refs += "UNRESOLVED:$g" }
            } else { $refs += $r }
        }
    }
    [PSCustomObject]@{
        name = $j.name
        path = $f.FullName.Replace("$root\Assets\","")
        references = $refs
        editorOnly = ($j.includePlatforms -and $j.includePlatforms.Count -eq 1 -and $j.includePlatforms[0] -eq "Editor")
        defineConstraints = $j.defineConstraints
        autoReferenced = $j.autoReferenced
    }
}
$graph | ConvertTo-Json -Depth 5 | Set-Content "$OutDir\graph$Suffix.json" -Encoding utf8
"asmdefs: $($graph.Count)"
$asmrefs = Get-ChildItem -Recurse -Filter *.asmref -Path "$root\Assets" -ErrorAction SilentlyContinue
$refList = foreach ($f in $asmrefs) {
    $j = Get-Content $f.FullName -Raw | ConvertFrom-Json
    $target = $j.reference
    if ($target -like "GUID:*") { $g = $target.Substring(5); if ($guidMap.ContainsKey($g)) { $target = $guidMap[$g] } }
    [PSCustomObject]@{ path = $f.FullName.Replace("$root\Assets\",""); target = $target }
}
$refList | ConvertTo-Json | Set-Content "$OutDir\asmrefs$Suffix.json" -Encoding utf8
"asmrefs: $($asmrefs.Count)"
$unresolvedEdges = ($graph | ForEach-Object { $_.references } | Where-Object { $_ -like "UNRESOLVED:*" })
"unresolved edges: $(($unresolvedEdges | Measure-Object).Count); distinct: $(($unresolvedEdges | Sort-Object -Unique | Measure-Object).Count)"
"output: $OutDir"
