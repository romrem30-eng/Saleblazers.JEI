[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'

function Write-Banner {
    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host "             Saleblazers JEI Mod Installer                " -ForegroundColor Yellow
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Find-SaleblazersPath {
    $candidates = [System.Collections.Generic.List[string]]::new()

    # 1. Check Steam Registry and libraryfolders.vdf
    try {
        $steamKey = Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
        if ($steamKey -and $steamKey.SteamPath) {
            $steamPath = $steamKey.SteamPath.Replace('/', '\')
            $candidates.Add((Join-Path $steamPath 'steamapps\common\Saleblazers\Default'))
            $candidates.Add((Join-Path $steamPath 'steamapps\common\Saleblazers'))

            $vdfPath = Join-Path $steamPath 'steamapps\libraryfolders.vdf'
            if (Test-Path $vdfPath) {
                $lines = Get-Content $vdfPath -Encoding UTF8
                foreach ($line in $lines) {
                    if ($line -match '"path"\s+"([^"]+)"') {
                        $lib = $matches[1].Replace('\\', '\')
                        $candidates.Add((Join-Path $lib 'steamapps\common\Saleblazers\Default'))
                        $candidates.Add((Join-Path $lib 'steamapps\common\Saleblazers'))
                    }
                }
            }
        }
    } catch {}

    # 2. Check common drive paths
    $commonDrives = @('C', 'D', 'E', 'F', 'G')
    foreach ($d in $commonDrives) {
        $candidates.Add("$d`:\Program Files (x86)\Steam\steamapps\common\Saleblazers\Default")
        $candidates.Add("$d`:\Program Files\Steam\steamapps\common\Saleblazers\Default")
        $candidates.Add("$d`:\SteamLibrary\steamapps\common\Saleblazers\Default")
        $candidates.Add("$d`:\Steam\steamapps\common\Saleblazers\Default")
        $candidates.Add("$d`:\Games\Saleblazers\Default")
    }

    # 3. Test each candidate for Saleblazers.exe
    foreach ($cand in $candidates) {
        if ([string]::IsNullOrWhiteSpace($cand)) { continue }
        $exe = Join-Path $cand 'Saleblazers.exe'
        if (Test-Path $exe) {
            return $cand
        }
    }

    return $null
}

Write-Banner

Write-Host "[1/4] Detecting Saleblazers installation..." -ForegroundColor White
$gamePath = Find-SaleblazersPath

if (-not $gamePath) {
    Write-Host "Could not automatically locate Saleblazers directory." -ForegroundColor Yellow
    Write-Host "Please paste your Saleblazers installation path:" -ForegroundColor Yellow
    Write-Host "Example: C:\Program Files (x86)\Steam\steamapps\common\Saleblazers\Default" -ForegroundColor Gray
    Write-Host ""
    $userInput = Read-Host "Game Path"
    if ($userInput) {
        $cleanInput = $userInput.Trim('"').Trim()
        if (Test-Path (Join-Path $cleanInput 'Saleblazers.exe')) {
            $gamePath = $cleanInput
        } elseif (Test-Path (Join-Path $cleanInput 'Default\Saleblazers.exe')) {
            $gamePath = Join-Path $cleanInput 'Default'
        }
    }
}

if (-not $gamePath -or -not (Test-Path (Join-Path $gamePath 'Saleblazers.exe'))) {
    Write-Host ""
    Write-Host "Error: Valid Saleblazers installation folder not found." -ForegroundColor Red
    Write-Host "Make sure the folder contains Saleblazers.exe." -ForegroundColor Red
    exit 1
}

Write-Host "Found game at: $gamePath" -ForegroundColor Green
Write-Host ""

Write-Host "[2/4] Checking BepInEx 6 IL2CPP installation..." -ForegroundColor White
$winHttp = Join-Path $gamePath 'winhttp.dll'
$bepCore = Join-Path $gamePath 'BepInEx\core\BepInEx.Core.dll'

if (-not (Test-Path $winHttp) -or -not (Test-Path $bepCore)) {
    Write-Host "BepInEx 6 IL2CPP is not detected. Downloading pre-configured Modding Starter Kit..." -ForegroundColor Yellow
    $bepUrl = "https://github.com/romrem30-eng/Saleblazers.ModdingStarterKit/releases/download/v1.0.0/Saleblazers.BepInExPack-v6.0.0.zip"
    $tempZip = Join-Path $env:TEMP "Saleblazers_BepInExPack_v6.0.0.zip"

    try {
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
        Write-Host "Downloading from GitHub..." -ForegroundColor Gray
        Invoke-WebRequest -Uri $bepUrl -OutFile $tempZip -UseBasicParsing
        Write-Host "Extracting BepInEx to $gamePath..." -ForegroundColor Gray
        Expand-Archive -Path $tempZip -DestinationPath $gamePath -Force
        Remove-Item -Path $tempZip -Force -ErrorAction SilentlyContinue
        Write-Host "BepInEx 6 IL2CPP installed successfully." -ForegroundColor Green
    } catch {
        Write-Host "Failed to automatically download BepInEx: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Please download BepInEx 6 Unity.IL2CPP win-x64 manually and unpack to your game folder." -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "BepInEx 6 IL2CPP is already installed." -ForegroundColor Green
}
Write-Host ""

Write-Host "[3/4] Installing Saleblazers.JEI..." -ForegroundColor White
$pluginsDir = Join-Path $gamePath 'BepInEx\plugins'
if (-not (Test-Path $pluginsDir)) {
    New-Item -Path $pluginsDir -ItemType Directory -Force | Out-Null
}

$scriptFolder = $PSScriptRoot
if ([string]::IsNullOrEmpty($scriptFolder)) {
    $scriptFolder = (Get-Location).Path
}

$candidatesDll = @(
    (Join-Path $scriptFolder 'Saleblazers.JEI.dll'),
    (Join-Path $scriptFolder 'bin\Release\Saleblazers.JEI.dll'),
    (Join-Path $scriptFolder 'bin\Debug\Saleblazers.JEI.dll')
)

$sourceDll = $null
foreach ($c in $candidatesDll) {
    if (Test-Path $c) {
        $sourceDll = $c
        break
    }
}

if (-not $sourceDll) {
    Write-Host "Error: Could not find Saleblazers.JEI.dll in current directory." -ForegroundColor Red
    exit 1
}

$destDll = Join-Path $pluginsDir 'Saleblazers.JEI.dll'
Copy-Item -Path $sourceDll -Destination $destDll -Force
Write-Host "Successfully installed Saleblazers.JEI.dll -> $destDll" -ForegroundColor Green
Write-Host ""

Write-Host "[4/4] Installation Complete!" -ForegroundColor Cyan
Write-Host "Controls in-game:" -ForegroundColor Yellow
Write-Host "  * J or F8        - Toggle JEI Catalog" -ForegroundColor White
Write-Host "  * R (hover item) - View recipes to craft this item" -ForegroundColor White
Write-Host "  * U (hover item) - View recipes where this item is used" -ForegroundColor White
Write-Host "  * Click Station  - Jump directly to that station's crafts" -ForegroundColor White
Write-Host "  * Click Lang     - Toggle RU / EN language live" -ForegroundColor White
Write-Host "  * ESC            - Close JEI window" -ForegroundColor White
Write-Host ""
