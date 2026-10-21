param(
    [string]$Filter = "PoisonDamagePreviewTests",
    [string]$Sts2Path = "",
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $projectRoot "DefenseAwareDebuffPreview.Tests\DefenseAwareDebuffPreview.Tests.csproj"
$testRoot = "C:\tmp\sts2-combat-tests\DefenseAwareDebuffPreview_tests"
$logDir = Join-Path $testRoot "logs"
$stdoutLog = Join-Path $logDir "run-windows.log"
$stderrLog = Join-Path $logDir "run-windows.err.log"

if ([string]::IsNullOrWhiteSpace($Sts2Path)) {
    $registryPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 2868840"
    $Sts2Path = (Get-ItemProperty -Path $registryPath -ErrorAction SilentlyContinue).InstallLocation
}

if ([string]::IsNullOrWhiteSpace($Sts2Path)) {
    $Sts2Path = "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2"
}

$executable = Join-Path $Sts2Path "SlayTheSpire2.exe"
if (-not (Test-Path -LiteralPath $executable)) {
    throw "SlayTheSpire2.exe was not found at '$executable'. Pass -Sts2Path explicitly."
}

$msbuildArgs = @(
    "msbuild",
    $testProject,
    "-restore",
    "-t:InstallSts2TestMod",
    "-p:Sts2Path=$Sts2Path",
    "-v:minimal"
)
& dotnet @msbuildArgs
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build and install the TestTheSpire payload."
}

New-Item -ItemType Directory -Path $logDir -Force | Out-Null

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $executable
$startInfo.UseShellExecute = $false
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.CreateNoWindow = $true
$startInfo.Environment["APPDATA"] = $testRoot
$startInfo.ArgumentList.Add("--headless")
$startInfo.ArgumentList.Add("--force-steam=off")
$startInfo.ArgumentList.Add("--sts2-test")
$startInfo.ArgumentList.Add("--sts2-test-filter=$Filter")

$process = [System.Diagnostics.Process]::new()
$process.StartInfo = $startInfo
[void]$process.Start()
$stdoutTask = $process.StandardOutput.ReadToEndAsync()
$stderrTask = $process.StandardError.ReadToEndAsync()

if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    $process.Kill($true)
    throw "STS2 tests timed out after $TimeoutSeconds seconds."
}

$stdout = $stdoutTask.GetAwaiter().GetResult()
$stderr = $stderrTask.GetAwaiter().GetResult()
[System.IO.File]::WriteAllText($stdoutLog, $stdout)
[System.IO.File]::WriteAllText($stderrLog, $stderr)

$summary = $stdout -split "`r?`n" | Where-Object { $_ -match " SUMMARY total=" } | Select-Object -Last 1
if ($summary) {
    Write-Host $summary
}

if (-not $summary -or $summary -notmatch " failed=0 ") {
    Write-Host "Full stdout: $stdoutLog"
    Write-Host "Full stderr: $stderrLog"
    exit 1
}

Write-Host "Full stdout: $stdoutLog"
exit 0
