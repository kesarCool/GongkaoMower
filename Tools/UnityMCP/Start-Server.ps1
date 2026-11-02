param([int]$Port = 8767)
$ErrorActionPreference = 'Stop'
$taskInstallation = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'installation.json') -Raw | ConvertFrom-Json
$taskBaseUrl = 'http://127.0.0.1:' + $Port
try {
    $taskHealth = Invoke-RestMethod -Uri ($taskBaseUrl + '/health') -TimeoutSec 2
    if ($taskHealth.status -eq 'healthy' -and $taskHealth.version -eq $taskInstallation.packageVersion) {
        Write-Output ('Already running: ' + $taskBaseUrl + '/mcp')
        exit 0
    }
    throw 'A different service is using the configured port.'
} catch {
    if ($_.Exception.Message -like '*different service*') { throw }
}
$taskLogs = Join-Path $taskInstallation.cache 'logs'
New-Item -ItemType Directory -Path $taskLogs -Force | Out-Null
$env:UNITY_MCP_TELEMETRY_ENABLED = 'false'
$env:UNITY_MCP_LOG_DIR = $taskLogs
$taskArguments = @('--transport', 'http', '--http-url', $taskBaseUrl, '--default-instance', 'GongkaoMower', '--pidfile', ('"' + (Join-Path $taskInstallation.cache 'server.pid') + '"'))
$taskProcess = Start-Process -FilePath $taskInstallation.mcpServer -ArgumentList $taskArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskLogs 'stdout.log') -RedirectStandardError (Join-Path $taskLogs 'stderr.log')
Write-Output ('Started local Unity MCP, PID ' + $taskProcess.Id + ', ' + $taskBaseUrl + '/mcp')
