# Installs Cloudflare Tunnel Manager for the current user (no administrator rights, no .NET required).
# Ships in the archive next to the app\ folder. Easiest to run via install.cmd.
#
#   install.cmd              install or update
#   install.cmd -Autostart   also start on login
#   install.cmd -Uninstall   remove again (profiles and logs in %APPDATA%\CloudflareTray are kept)
#
# Uninstalling also works via Settings > Apps.
param(
    [switch]$Autostart,
    [switch]$Uninstall
)
$ErrorActionPreference = 'Stop'

$Version      = '@VERSION@'
$DisplayName  = 'Cloudflare Tunnel Manager'
$AppDir       = Join-Path $env:LOCALAPPDATA 'Programs\CloudflareTray'
$Exe          = Join-Path $AppDir 'CloudflareTray.exe'
$Shortcut     = Join-Path ([Environment]::GetFolderPath('Programs')) "$DisplayName.lnk"
$RunKey       = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$UninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CloudflareTray'

function Stop-Running {
    # The app only stops its cloudflared processes when it exits normally.
    # When it is stopped from outside, stop the child processes too, otherwise the tunnels keep running orphaned.
    foreach ($process in Get-Process -Name CloudflareTray -ErrorAction SilentlyContinue) {
        Get-CimInstance Win32_Process -Filter "ParentProcessId = $($process.Id)" |
            ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Write-Host 'Stopped the running Cloudflare Tunnel Manager.'
    }
    Start-Sleep -Seconds 1
}

if ($Uninstall) {
    Stop-Running
    Remove-ItemProperty -Path $RunKey -Name CloudflareTray -ErrorAction SilentlyContinue
    Remove-Item -Path $UninstallKey -Recurse -ErrorAction SilentlyContinue
    Remove-Item -Path $Shortcut -ErrorAction SilentlyContinue
    Remove-Item -Path $AppDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "$DisplayName removed. Profiles and logs are still in $env:APPDATA\CloudflareTray."
    return
}

$Source = Join-Path $PSScriptRoot 'app'
if (-not (Test-Path (Join-Path $Source 'CloudflareTray.exe'))) {
    throw 'app\CloudflareTray.exe is missing – please run install.cmd from the unpacked archive.'
}

Stop-Running
if (Test-Path $AppDir) { Remove-Item -Path $AppDir -Recurse -Force }
New-Item -ItemType Directory -Path $AppDir -Force | Out-Null
Copy-Item -Path (Join-Path $Source '*') -Destination $AppDir -Recurse -Force
# For uninstalling via Settings > Apps
Copy-Item -Path $PSCommandPath -Destination (Join-Path $AppDir 'install.ps1') -Force

# Files downloaded from the internet are marked; without unblocking, Windows warns on every launch
Get-ChildItem -Path $AppDir -Recurse -File | Unblock-File

$Icon = "$Exe,0"
$Shell = New-Object -ComObject WScript.Shell
$Link = $Shell.CreateShortcut($Shortcut)
$Link.TargetPath = $Exe
$Link.WorkingDirectory = $AppDir
$Link.IconLocation = $Icon
$Link.Description = 'Start and monitor Cloudflare Access tunnels from the tray'
$Link.Save()

New-Item -Path $UninstallKey -Force | Out-Null
$Uninstaller = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$AppDir\install.ps1`" -Uninstall"
Set-ItemProperty -Path $UninstallKey -Name DisplayName -Value $DisplayName
Set-ItemProperty -Path $UninstallKey -Name DisplayVersion -Value $Version
Set-ItemProperty -Path $UninstallKey -Name DisplayIcon -Value $Icon
Set-ItemProperty -Path $UninstallKey -Name InstallLocation -Value $AppDir
Set-ItemProperty -Path $UninstallKey -Name UninstallString -Value $Uninstaller
Set-ItemProperty -Path $UninstallKey -Name NoModify -Value 1 -Type DWord
Set-ItemProperty -Path $UninstallKey -Name NoRepair -Value 1 -Type DWord

if ($Autostart) {
    Set-ItemProperty -Path $RunKey -Name CloudflareTray -Value "`"$Exe`""
    Write-Host 'Autostart set up.'
}

Write-Host "Installed – find it in the Start menu as `"$DisplayName`"."
if (-not (Get-Command cloudflared -ErrorAction SilentlyContinue)) {
    Write-Host 'Note: cloudflared is not installed: winget install --id Cloudflare.cloudflared'
}
