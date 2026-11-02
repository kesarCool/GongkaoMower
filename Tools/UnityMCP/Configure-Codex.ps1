$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskConfigDir = Join-Path $taskRoot '.codex'
$taskConfigPath = Join-Path $taskConfigDir 'config.toml'
$taskSection = '[mcp_servers.unity_gongkaomower]'
$taskBlock = @'
[mcp_servers.unity_gongkaomower]
url = "http://127.0.0.1:8767/mcp"
startup_timeout_sec = 60
tool_timeout_sec = 60
enabled = true
'@
$taskExisting = ''
if (Test-Path -LiteralPath $taskConfigPath) {
    $taskExisting = [IO.File]::ReadAllText($taskConfigPath)
    if ($taskExisting.Contains($taskSection)) {
        Write-Output 'Project MCP configuration already exists; left unchanged.'
        exit 0
    }
    Copy-Item -LiteralPath $taskConfigPath -Destination (Join-Path $PSScriptRoot ('codex-config.before-' + (Get-Date -Format 'yyyyMMddHHmmss') + '.toml'))
}
New-Item -ItemType Directory -Path $taskConfigDir -Force | Out-Null
[IO.File]::WriteAllText($taskConfigPath, $taskExisting.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine + $taskBlock + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Output ('Configured project MCP: ' + $taskConfigPath)
