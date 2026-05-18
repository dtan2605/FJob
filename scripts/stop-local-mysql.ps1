$configPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.local\mysql\my.ini"))
$process = Get-CimInstance Win32_Process -Filter "name='mysqld.exe'" |
    Where-Object { $_.CommandLine -like "*$configPath*" } |
    Select-Object -First 1

if (-not $process) {
    Write-Host "Local MySQL is not running."
    exit 0
}

Stop-Process -Id $process.ProcessId -Force
Write-Host "Stopped local MySQL process $($process.ProcessId)."
