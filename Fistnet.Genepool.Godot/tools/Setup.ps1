[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-IfChanged([string] $Path, [string] $Text) {
    if (-not [IO.File]::Exists($Path) -or [IO.File]::ReadAllText($Path) -cne $Text) {
        [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
    }
}

$viewerRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $viewerRoot
$editor = Join-Path $repoRoot '.tools\godot\4.7.2\Godot_v4.7.2-stable_mono_win64.exe'
if (-not (Test-Path -LiteralPath $editor -PathType Leaf)) {
    throw 'Godot .NET 4.7.2 is missing. Follow the download instructions in the viewer README.'
}
$processData = Join-Path $repoRoot '.tools\godot-process'
$roaming = Join-Path $processData 'roaming'
$local = Join-Path $processData 'local'
$temporary = Join-Path $processData 'tmp'
$dotnetData = Join-Path $processData 'dotnet'
foreach ($directory in @($roaming, $local, $temporary, $dotnetData)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
$profile = [ordered]@{
    profiles = [ordered]@{
        'Genepool (Godot)' = [ordered]@{
            commandName = 'Executable'
            executablePath = $editor
            commandLineArgs = '--path "' + $viewerRoot + '" --rendering-method gl_compatibility'
            workingDirectory = $viewerRoot
            nativeDebugging = $true
            environmentVariables = [ordered]@{
                APPDATA = $roaming
                LOCALAPPDATA = $local
                TEMP = $temporary
                TMP = $temporary
                DOTNET_CLI_HOME = $dotnetData
                DOTNET_CLI_TELEMETRY_OPTOUT = '1'
                DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
                DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
                NUGET_PACKAGES = (Join-Path $repoRoot '.tools\nuget\packages')
            }
        }
    }
}
$properties = Join-Path $viewerRoot 'Properties'
New-Item -ItemType Directory -Path $properties -Force | Out-Null
$json = $profile | ConvertTo-Json -Depth 6
Write-IfChanged (Join-Path $properties 'launchSettings.json') ($json + [Environment]::NewLine)

# Godot 4.7.2: ExternalEditorId.VisualStudio = 1; this is the portable editor's settings file.
# https://github.com/godotengine/godot/blob/4.7.2-stable/modules/mono/editor/GodotTools/GodotTools/ExternalEditorId.cs
$settingsPath = Join-Path (Split-Path -Parent $editor) 'editor_data\editor_settings-4.7.tres'
if ([IO.File]::Exists($settingsPath)) {
    $settings = [IO.File]::ReadAllText($settingsPath)
    $settingPattern = '(?m)^dotnet/editor/external_editor\s*=\s*[^\r\n]*'
    $settingMatches = [regex]::Matches($settings, $settingPattern)
    if ($settingMatches.Count -gt 1) {
        throw 'The portable editor settings contain duplicate external-editor entries; resolve them before setup.'
    }
    if ($settingMatches.Count -eq 1) {
        $settings = [regex]::Replace($settings, $settingPattern, 'dotnet/editor/external_editor = 1')
    } else {
        $resourcePattern = '(?m)^\[resource\]\r?$'
        if ([regex]::Matches($settings, $resourcePattern).Count -ne 1) {
            throw 'The portable editor settings have no unique resource section.'
        }
        $newline = if ($settings.Contains("`r`n")) { "`r`n" } else { "`n" }
        $settings = [regex]::Replace($settings, $resourcePattern,
            ('[resource]' + $newline + 'dotnet/editor/external_editor = 1'))
    }
    Write-IfChanged $settingsPath $settings
    Write-Output 'Portable Godot C# external editor: Visual Studio.'
} else {
    Write-Output 'Portable editor settings do not exist yet. Close Godot and rerun Setup after its first editor launch.'
}

Write-Output 'Local Visual Studio profile ready. Select Debug, the Godot startup project, and Genepool (Godot).'
