# apply_agentic_migrations.ps1
# Usage: run in repo root from PowerShell
# This script stops Easshas.WebApi, backs up the manual migration (if present), builds, ensures migrations are visible,
# scaffolds AddAgenticEntities_Auto if missing, and applies migrations to the Development DB.

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
# Project root is the parent of the scripts directory
$ProjectRoot = Resolve-Path (Join-Path $ScriptDir "..")
Set-Location $ProjectRoot

Write-Host "Stopping Easshas.WebApi processes (if any) ..."
Get-Process -Name Easshas.WebApi -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }

# Ensure Development environment is used for EF commands
$env:ASPNETCORE_ENVIRONMENT = "Development"

Write-Host "Cleaning and building solution..."
dotnet clean
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet clean failed"; exit 1 }

dotnet build
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet build failed"; exit 1 }

# Backup manual migration file if it exists (to avoid EF conflicts)
$manualMigration = "src\Easshas.Infrastructure\Migrations\20260310_AddAgenticEntities.cs"
if (Test-Path $manualMigration) {
    $backupDir = "src\Easshas.Infrastructure\Migrations\backup_$(Get-Date -Format yyyyMMdd_HHmmss)"
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    Move-Item $manualMigration $backupDir
    Write-Host "Moved manual migration to $backupDir"
}

# List migrations visible to EF
Write-Host "Listing migrations visible to EF..."
$migList = dotnet ef migrations list -p src\Easshas.Infrastructure -s src\Easshas.WebApi --no-build 2>&1
Write-Host $migList

if ($migList -notmatch 'AddAgenticEntities' -and $migList -notmatch 'AddAgenticEntities_Auto') {
    Write-Host "AddAgenticEntities not found in list. Creating migration AddAgenticEntities_Auto..."
    dotnet ef migrations add AddAgenticEntities_Auto -p src\Easshas.Infrastructure -s src\Easshas.WebApi
    if ($LASTEXITCODE -ne 0) { Write-Error "Failed to create migration"; exit 1 }
} else {
    Write-Host "AddAgenticEntities migration already present in EF migrations list."
}

# Ensure POSTGRES_PASSWORD is set if connection string relies on it
if (-not $env:POSTGRES_PASSWORD) {
    $pwd = Read-Host -AsSecureString "POSTGRES_PASSWORD not set. Enter DB password for Development connection (will not be stored)"
    if ($pwd.Length -eq 0) {
        Write-Error "No password provided; aborting."
        exit 1
    }
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($pwd)
    $plainPwd = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)
    $env:POSTGRES_PASSWORD = $plainPwd
}

Write-Host "Applying migrations to Development DB..."
dotnet ef database update -p src\Easshas.Infrastructure -s src\Easshas.WebApi --verbose
if ($LASTEXITCODE -ne 0) { Write-Error "database update failed"; exit 1 }

Write-Host "Migrations applied. You can now run: dotnet run --project src\Easshas.WebApi"
Write-Host "If you backed up a manual migration file, check the backup folder under src/Easshas.Infrastructure/Migrations."