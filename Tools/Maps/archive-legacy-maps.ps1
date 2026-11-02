$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).ProviderPath
if ($taskRoot -ne 'G:\Demo\GongkaoMower') { throw 'Unexpected workspace root.' }
$taskPlanPath = Join-Path $taskRoot 'ArtOutput\MapCleanup\cleanup-plan.json'
if (Test-Path -LiteralPath (Join-Path $taskRoot 'ArtOutput\MapCleanup\archive-manifest.json')) { throw 'This audited cleanup has already run. Do not reuse its stale plan for a new cleanup.' }
$taskPlan = Get-Content -LiteralPath $taskPlanPath -Raw | ConvertFrom-Json
$taskAllowedRoots = @(
    ([IO.Path]::GetFullPath((Join-Path $taskRoot 'Assets\Res\Map')) + '\')
    ([IO.Path]::GetFullPath((Join-Path $taskRoot 'Assets\mapRes\Prefab')) + '\')
)
$taskArchiveRoot = [IO.Path]::GetFullPath((Join-Path $taskRoot ('ArtOutput\MapCleanup\ArchivedLegacyMaps-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))))
if (-not $taskArchiveRoot.StartsWith(($taskRoot + '\ArtOutput\MapCleanup\'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Archive escaped its intended directory.' }
if (Test-Path -LiteralPath $taskArchiveRoot) { throw 'Archive destination already exists.' }
$taskRecords = @()
foreach ($taskAsset in $taskPlan.candidates) {
    $taskSource = [IO.Path]::GetFullPath((Join-Path $taskRoot $taskAsset.path))
    $taskAllowed = $false
    foreach ($taskAllowedRoot in $taskAllowedRoots) {
        if ($taskSource.StartsWith($taskAllowedRoot, [StringComparison]::OrdinalIgnoreCase)) { $taskAllowed = $true }
    }
    if (-not $taskAllowed) { throw ('Asset is outside the audited cleanup scope: ' + $taskSource) }
    if ($taskSource.EndsWith('\common_tree.png', [StringComparison]::OrdinalIgnoreCase)) { throw 'Shared wall texture must be retained.' }
    foreach ($taskSuffix in @('', '.meta')) {
        $taskFile = $taskSource + $taskSuffix
        if (-not (Test-Path -LiteralPath $taskFile -PathType Leaf)) { throw ('Missing audited file: ' + $taskFile) }
        $taskRelative = $taskAsset.path + $taskSuffix
        $taskDestination = [IO.Path]::GetFullPath((Join-Path $taskArchiveRoot $taskRelative))
        if (-not $taskDestination.StartsWith(($taskArchiveRoot + '\'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Archive path escaped its root.' }
        $taskRecords += [pscustomobject]@{ originalPath = $taskRelative; archivePath = $taskDestination; bytes = (Get-Item -LiteralPath $taskFile).Length; sha256 = (Get-FileHash -LiteralPath $taskFile -Algorithm SHA256).Hash }
    }
}
New-Item -ItemType Directory -Path $taskArchiveRoot | Out-Null
$taskManifest = [pscustomobject]@{ archiveRoot = $taskArchiveRoot; assetCount = $taskPlan.candidates.Count; fileCount = $taskRecords.Count; records = $taskRecords }
$taskManifestPath = Join-Path $taskRoot 'ArtOutput\MapCleanup\archive-manifest.json'
$taskManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $taskManifestPath -Encoding UTF8
foreach ($taskRecord in $taskRecords) {
    $taskOriginal = Join-Path $taskRoot $taskRecord.originalPath
    New-Item -ItemType Directory -Path (Split-Path -Parent $taskRecord.archivePath) -Force | Out-Null
    Move-Item -LiteralPath $taskOriginal -Destination $taskRecord.archivePath
    if ((Get-FileHash -LiteralPath $taskRecord.archivePath -Algorithm SHA256).Hash -ne $taskRecord.sha256) { throw ('Archive hash mismatch: ' + $taskRecord.originalPath) }
}
$taskManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskArchiveRoot 'archive-manifest.json') -Encoding UTF8
[pscustomobject]@{ archiveRoot = $taskArchiveRoot; assetsArchived = $taskManifest.assetCount; filesArchived = $taskManifest.fileCount; sourceBytesArchived = ($taskRecords | Measure-Object -Property bytes -Sum).Sum } | ConvertTo-Json
