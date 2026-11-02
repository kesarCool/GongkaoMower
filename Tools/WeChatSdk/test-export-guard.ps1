param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw "C# compiler not found: $compiler" }
$testDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('WeChatExportGuard-' + [Guid]::NewGuid().ToString('N'))
[void][System.IO.Directory]::CreateDirectory($testDirectory)
$testExe = Join-Path $testDirectory 'ExportGuardTests.exe'
try {
    & $compiler /nologo /target:exe /r:System.Web.Extensions.dll "/out:$testExe" `
        (Join-Path $ProjectRoot 'Assets/WX-WASM-SDK-V2/Editor/WXBridgeValidator.cs') `
        (Join-Path $PSScriptRoot 'ExportGuardTests.cs')
    if ($LASTEXITCODE -ne 0) { throw "Export guard test compilation failed ($LASTEXITCODE)." }
    & $testExe $ProjectRoot
    if ($LASTEXITCODE -ne 0) { throw "Export guard tests failed ($LASTEXITCODE)." }
}
finally {
    # Delete only the exact two generated files, then the now-empty unique directory.
    if (Test-Path -LiteralPath $testExe) { Remove-Item -LiteralPath $testExe }
    [System.IO.Directory]::Delete($testDirectory)
}
