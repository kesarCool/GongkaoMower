param(
    [string]$ProjectRoot = 'G:\Demo\GongkaoMower'
)
$ErrorActionPreference = 'Stop'
$taskVersion = '10.3.0'
$taskCommit = 'ba6881930eadb2a9006d28bff3325ce03b4bfd8a'
$taskPython = 'C:\Users\Admin\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$taskRoot = [IO.Path]::GetFullPath($ProjectRoot)
$taskCache = [IO.Path]::GetFullPath((Join-Path $taskRoot 'Library\UnityMCPTools'))
if (-not $taskCache.StartsWith($taskRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Cache is outside project' }
$taskDownloads = Join-Path $taskCache 'downloads'
$taskSourceRoot = Join-Path $taskCache 'source'
$taskSource = Join-Path $taskSourceRoot ('unity-mcp-' + $taskCommit)
$taskUvPrefix = Join-Path $taskCache 'uv'
$taskUv = Join-Path $taskUvPrefix 'Scripts\uv.exe'
$taskVenv = Join-Path $taskCache 'server-venv'
$taskVenvPython = Join-Path $taskVenv 'Scripts\python.exe'
foreach ($taskDir in @($taskCache,$taskDownloads,$taskSourceRoot)) { New-Item -ItemType Directory -Path $taskDir -Force | Out-Null }
$env:UV_CACHE_DIR = Join-Path $taskCache 'uv-cache'
$env:UV_PYTHON_INSTALL_DIR = Join-Path $taskCache 'python'
$env:PIP_CACHE_DIR = Join-Path $taskCache 'pip-cache'
$env:UV_LINK_MODE = 'copy'

if (-not (Test-Path -LiteralPath (Join-Path $taskSource 'MCPForUnity\package.json'))) {
    $taskArchive = Join-Path $taskDownloads ('unity-mcp-' + $taskCommit + '.zip')
    Write-Output 'Downloading pinned Unity MCP source...'
    Invoke-WebRequest -Uri ('https://codeload.github.com/CoplayDev/unity-mcp/zip/' + $taskCommit) -OutFile $taskArchive
    Expand-Archive -LiteralPath $taskArchive -DestinationPath $taskSourceRoot -Force
}
$taskPackage = Get-Content -LiteralPath (Join-Path $taskSource 'MCPForUnity\package.json') -Raw | ConvertFrom-Json
if ($taskPackage.version -ne $taskVersion) { throw 'Downloaded package version does not match' }

if (-not (Test-Path -LiteralPath $taskUv)) {
    Write-Output 'Installing uv into project cache...'
    & $taskPython -m pip install --disable-pip-version-check --no-warn-script-location --prefix $taskUvPrefix uv
    if ($LASTEXITCODE -ne 0) { throw 'uv installation failed' }
}
if (-not (Test-Path -LiteralPath $taskVenvPython)) {
    & $taskUv venv --python $taskPython $taskVenv
    if ($LASTEXITCODE -ne 0) { throw 'Server virtual environment creation failed' }
}
Write-Output 'Installing pinned Unity MCP server and its dependencies...'
& $taskUv pip install --python $taskVenvPython (Join-Path $taskSource 'Server')
if ($LASTEXITCODE -ne 0) { throw 'Unity MCP server installation failed' }
& $taskUv --version
& $taskVenvPython -c 'from importlib.metadata import version; print("mcpforunityserver=" + version("mcpforunityserver"))'
if ($LASTEXITCODE -ne 0) { throw 'Server version verification failed' }
$taskResult = [ordered]@{
    packageVersion=$taskVersion
    commit=$taskCommit
    source=$taskSource
    uv=$taskUv
    python=$taskVenvPython
    mcpServer=(Join-Path $taskVenv 'Scripts\mcp-for-unity.exe')
    cache=$taskCache
}
$taskResult | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskRoot 'Tools\UnityMCP\installation.json') -Encoding utf8
$taskResult | ConvertTo-Json
