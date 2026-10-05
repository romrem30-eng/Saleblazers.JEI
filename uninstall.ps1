[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'

function Write-Banner {
    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host "             Saleblazers JEI Mod Uninstaller              " -ForegroundColor Yellow
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Find-SaleblazersPath {
    $candidates = [System.Collections.Generic.List[string]]::new()

    try {
        $steamKey = Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
        if ($steamKey -and $steamKey.SteamPath) {
            $steamPath = $steamKey.SteamPath.Replace('/', '\')
            $candidates.Add((Join-Path $steamPath 'steamapps\common\Saleblazers\Default'))
            $candidates.Add((Join-Path $steamPath 'steamapps\common\Saleblazers'))

            $vdfPath = Join-Path $steamPath 'steamapps\libraryfolders.vdf'
            if (Test-Path $vdfPath) {
                $lines = Get-Content $vdfPath -Encoding UTF8 -ErrorAction SilentlyContinue
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

    $drives = Get-PSDrive -PSProvider FileSystem | Select-Object -ExpandProperty Root
    foreach ($d in $drives) {
        $candidates.Add((Join-Path $d 'Program Files (x86)\Steam\steamapps\common\Saleblazers\Default'))
        $candidates.Add((Join-Path $d 'Program Files\Steam\steamapps\common\Saleblazers\Default'))
        $candidates.Add((Join-Path $d 'SteamLibrary\steamapps\common\Saleblazers\Default'))
        $candidates.Add((Join-Path $d 'Steam\steamapps\common\Saleblazers\Default'))
        $candidates.Add((Join-Path $d 'Games\Saleblazers\Default'))
    }

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

Write-Host "Поиск игры..." -ForegroundColor White
$gamePath = Find-SaleblazersPath

if (-not $gamePath) {
    Write-Host "Укажите путь к папке игры (где находится Saleblazers.exe):" -ForegroundColor Yellow
    $userInput = Read-Host "Путь к игре"
    if ($userInput) {
        $clean = $userInput.Trim('"').Trim()
        if (Test-Path (Join-Path $clean 'Saleblazers.exe')) {
            $gamePath = $clean
        } elseif (Test-Path (Join-Path $clean 'Default\Saleblazers.exe')) {
            $gamePath = Join-Path $clean 'Default'
        }
    }
}

if (-not $gamePath -or -not (Test-Path (Join-Path $gamePath 'Saleblazers.exe'))) {
    Write-Host "[ОШИБКА] Папка игры не найдена." -ForegroundColor Red
    exit 1
}

Write-Host "Игра найдена: $gamePath" -ForegroundColor Green
Write-Host ""

$pluginsDir = Join-Path $gamePath 'BepInEx\plugins'
$jeiDll = Join-Path $pluginsDir 'Saleblazers.JEI.dll'
$jeiDisabled = Join-Path $pluginsDir 'Saleblazers.JEI.dll.disabled'

$found = $false
if (Test-Path $jeiDll) {
    Remove-Item $jeiDll -Force
    Write-Host "Удален файл: $jeiDll" -ForegroundColor Green
    $found = $true
}
if (Test-Path $jeiDisabled) {
    Remove-Item $jeiDisabled -Force
    Write-Host "Удален файл: $jeiDisabled" -ForegroundColor Green
    $found = $true
}

if (-not $found) {
    Write-Host "Saleblazers.JEI.dll не найден в папке BepInEx\plugins. Мод уже удален." -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "Мод Saleblazers JEI успешно удален!" -ForegroundColor Green
}
Write-Host ""
