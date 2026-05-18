$mysqlBin = "C:\Program Files\MySQL\MySQL Server 8.0\bin"
$mysqld = Join-Path $mysqlBin "mysqld.exe"
$mysql = Join-Path $mysqlBin "mysql.exe"

$baseDir = Join-Path $PSScriptRoot "..\.local\mysql"
$baseDir = [System.IO.Path]::GetFullPath($baseDir)
$dataDir = Join-Path $baseDir "data"
$logsDir = Join-Path $baseDir "logs"
$configPath = Join-Path $baseDir "my.ini"
$port = 3307

New-Item -ItemType Directory -Force -Path $dataDir, $logsDir | Out-Null

$config = @"
[mysqld]
basedir=C:/Program Files/MySQL/MySQL Server 8.0
datadir=$($dataDir -replace '\\','/')
port=$port
bind-address=127.0.0.1
log-error=$((Join-Path $logsDir 'error.log') -replace '\\','/')
pid-file=$((Join-Path $baseDir 'mysql.pid') -replace '\\','/')
"@

Set-Content -Path $configPath -Value $config -Encoding ASCII

if (-not (Test-Path (Join-Path $dataDir "mysql"))) {
    & $mysqld --initialize-insecure --basedir="C:/Program Files/MySQL/MySQL Server 8.0" --datadir=($dataDir -replace '\\','/')
}

$existing = Get-CimInstance Win32_Process -Filter "name='mysqld.exe'" |
    Where-Object { $_.CommandLine -like "*$configPath*" } |
    Select-Object -First 1

if (-not $existing) {
    Start-Process -FilePath $mysqld -ArgumentList "--defaults-file=$configPath --console" -WindowStyle Hidden | Out-Null
    Start-Sleep -Seconds 8
}

& $mysql --host=127.0.0.1 --port=$port --protocol=tcp --user=root -e "CREATE DATABASE IF NOT EXISTS FJobDb; CREATE USER IF NOT EXISTS 'fjob'@'%' IDENTIFIED BY 'FJobDb_123!'; GRANT ALL PRIVILEGES ON FJobDb.* TO 'fjob'@'%'; FLUSH PRIVILEGES;"

Write-Host "Local MySQL is ready on 127.0.0.1:$port"
Write-Host "Database: FJobDb"
Write-Host "User: fjob"
Write-Host "Password: FJobDb_123!"
