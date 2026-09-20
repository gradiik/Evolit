param(
    [string]$GodotPath = ""
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$BuildDir = Join-Path $RepoRoot ".local-build\windows"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\Evolit"
$Executable = Join-Path $InstallDir "Evolit.exe"

function Resolve-Godot {
    param([string]$ExplicitPath)

    if ($ExplicitPath -and (Test-Path $ExplicitPath)) {
        return (Resolve-Path $ExplicitPath).Path
    }

    foreach ($name in @("godot", "godot4")) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command) {
            return $command.Source
        }
    }

    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Godot\Godot_v4.4.1-stable_mono_win64.exe",
        "$env:ProgramFiles\Godot\Godot_v4.4.1-stable_mono_win64.exe",
        "$env:USERPROFILE\Downloads\Godot_v4.4.1-stable_mono_win64\Godot_v4.4.1-stable_mono_win64.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "Godot 4.4.1 .NET не найден. Передайте -GodotPath или установите Godot 4.4.1 .NET."
}

Write-Host "== Evolit local install ==" -ForegroundColor Cyan
Write-Host "Repository: $RepoRoot"

Push-Location $RepoRoot
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw ".NET SDK 8 не найден."
    }

    Write-Host "[1/5] Building C# project..."
    dotnet restore .\Evolit.csproj
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

    dotnet build .\Evolit.csproj --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }

    $Godot = Resolve-Godot -ExplicitPath $GodotPath
    Write-Host "Godot: $Godot"

    if (Test-Path $BuildDir) {
        Remove-Item $BuildDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $BuildDir -Force | Out-Null

    Write-Host "[2/5] Importing project..."
    & $Godot --headless --editor --path $RepoRoot --quit
    if ($LASTEXITCODE -ne 0) { throw "Godot project import failed." }

    Write-Host "[3/5] Exporting Windows test build..."
    $ExportExe = Join-Path $BuildDir "Evolit.exe"
    & $Godot --headless --path $RepoRoot --export-release "Windows Desktop" $ExportExe
    if ($LASTEXITCODE -ne 0) {
        throw "Godot export failed. Проверьте, что для Godot 4.4.1 установлены .NET export templates."
    }
    if (-not (Test-Path $ExportExe)) {
        throw "Export finished without Evolit.exe."
    }

    Write-Host "[4/5] Installing to $InstallDir ..."
    if (Test-Path $InstallDir) {
        Remove-Item $InstallDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Copy-Item (Join-Path $BuildDir "*") $InstallDir -Recurse -Force

    Write-Host "[5/5] Creating desktop shortcut..."
    $Desktop = [Environment]::GetFolderPath("Desktop")
    $ShortcutPath = Join-Path $Desktop "Evolit.lnk"
    $Shell = New-Object -ComObject WScript.Shell
    $Shortcut = $Shell.CreateShortcut($ShortcutPath)
    $Shortcut.TargetPath = $Executable
    $Shortcut.WorkingDirectory = $InstallDir
    $Shortcut.IconLocation = "$Executable,0"
    $Shortcut.Description = "Evolit local test build"
    $Shortcut.Save()

    Write-Host ""
    Write-Host "Installed: $Executable" -ForegroundColor Green
    Write-Host "Shortcut:  $ShortcutPath" -ForegroundColor Green

    Start-Process $Executable
}
finally {
    Pop-Location
}
