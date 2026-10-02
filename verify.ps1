param([ValidateSet('app', 'standalone', 'build')][string]$Package = 'app')
$ErrorActionPreference = 'Stop'
$taskApp = Join-Path $PSScriptRoot $(if ($Package -eq 'build') { 'src\TimeOnScreen\bin\Release\net10.0-windows\TimeOnScreen.exe' } else { "artifacts\$Package\TimeOnScreen.exe" })
$taskVerification = Join-Path $PSScriptRoot $(if ($Package -eq 'app') { 'artifacts\verification' } else { "artifacts\verification-$Package" })
$taskProcess = Start-Process -FilePath $taskApp -ArgumentList @('--verify', $taskVerification) -WindowStyle Hidden -PassThru
if (-not $taskProcess.WaitForExit(45000)) { throw 'Verification did not complete within 45 seconds' }
Get-Content -LiteralPath (Join-Path $taskVerification 'report.json')
exit $taskProcess.ExitCode
