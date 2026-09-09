[CmdletBinding(DefaultParameterSetName = 'Run', SupportsShouldProcess = $true)]
param(
    [Parameter(ParameterSetName = 'Run')]
    [switch] $Run,

    [Parameter(Mandatory = $true, ParameterSetName = 'Editor')]
    [switch] $Editor
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$viewerRoot = Split-Path -Parent $PSScriptRoot
$profilePath = Join-Path $viewerRoot 'Properties\launchSettings.json'
if (-not (Test-Path -LiteralPath $profilePath -PathType Leaf)) {
    throw 'Run tools\Setup.ps1 first to generate the local Visual Studio launch profile.'
}
$profile = (Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json).profiles.'Genepool (Godot)'
if ($profile.commandName -ne 'Executable' -or
    -not (Test-Path -LiteralPath $profile.executablePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $profile.workingDirectory -PathType Container)) {
    throw 'The local launch profile is invalid or has stale paths. Run tools\Setup.ps1 again.'
}
$debugAssembly = Join-Path $viewerRoot '.godot\mono\temp\bin\Debug\Fistnet.Genepool.Godot.dll'
if (-not (Test-Path -LiteralPath $debugAssembly -PathType Leaf)) {
    throw 'Build the Godot project in Debug using Fistnet.Genepool.slnx in Visual Studio 2026 before launching.'
}

$arguments = [string]$profile.commandLineArgs
if ($Editor) { $arguments = '--editor ' + $arguments }
$startInfo = [Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $profile.executablePath
$startInfo.Arguments = $arguments
$startInfo.WorkingDirectory = $profile.workingDirectory
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
# The caller explicitly requests an interactive viewer/editor window.
$startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Normal
foreach ($variable in $profile.environmentVariables.PSObject.Properties) {
    $startInfo.EnvironmentVariables[$variable.Name] = [string]$variable.Value
}

$mode = if ($Editor) { 'Godot scene editor' } else { 'Genepool viewer (Debug)' }
if ($PSCmdlet.ShouldProcess($startInfo.FileName, ('Launch ' + $mode + ' ' + $arguments))) {
    $process = [Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) { throw 'Godot did not start.' }
    Write-Output ($mode + ' started; process ' + $process.Id + '.')
    $process.Dispose()
}
