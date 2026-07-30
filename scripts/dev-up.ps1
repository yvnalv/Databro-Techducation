<#
.SYNOPSIS
    Brings up the DataBro local development stack and waits until it is ready.

.DESCRIPTION
    Default: infrastructure only (PostgreSQL, Redis, MinIO). Run the API and the Nuxt apps
    on the host for the fastest inner loop.

    -Apps: also builds and starts the containerised API and both Nuxt apps with hot reload.

.EXAMPLE
    ./scripts/dev-up.ps1
    ./scripts/dev-up.ps1 -Apps
    ./scripts/dev-up.ps1 -Reset          # destroys the local data volumes first
#>
[CmdletBinding()]
param(
    [switch]$Apps,
    [switch]$Reset
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Test-Path (Join-Path $root ".env"))) {
    Write-Host "No .env found - creating one from .env.example." -ForegroundColor Yellow
    Copy-Item (Join-Path $root ".env.example") (Join-Path $root ".env")
}

$composeArgs = @()
if ($Apps) { $composeArgs += @("--profile", "apps") }

if ($Reset) {
    Write-Host "Removing containers and data volumes..." -ForegroundColor Yellow
    docker compose @composeArgs down -v
}

Write-Host "Starting the DataBro stack..." -ForegroundColor Cyan
docker compose @composeArgs up -d --wait
if ($LASTEXITCODE -ne 0) { throw "docker compose up failed." }

docker compose @composeArgs ps --format "table {{.Name}}`t{{.Status}}`t{{.Ports}}"

# Read the published ports back out of .env so the printed URLs are always accurate.
$envMap = @{}
Get-Content (Join-Path $root ".env") |
    Where-Object { $_ -match "^\s*[^#].*=" } |
    ForEach-Object {
        $k, $v = $_.Split("=", 2)
        $envMap[$k.Trim()] = $v.Trim()
    }

function Get-Port($key, $fallback) {
    if ($envMap.ContainsKey($key) -and $envMap[$key]) { return $envMap[$key] }
    return $fallback
}

Write-Host ""
Write-Host "Ready." -ForegroundColor Green
Write-Host ("  PostgreSQL    localhost:{0}  (db/user: {1})" -f (Get-Port "POSTGRES_PORT" "5432"), (Get-Port "POSTGRES_DB" "databro"))
Write-Host ("  Redis         localhost:{0}" -f (Get-Port "REDIS_PORT" "6379"))
Write-Host ("  MinIO console http://localhost:{0}" -f (Get-Port "MINIO_CONSOLE_PORT" "9001"))

if ($Apps) {
    Write-Host ("  API           http://localhost:{0}/health" -f (Get-Port "API_PORT" "5158"))
    Write-Host ("  Site          http://localhost:{0}" -f (Get-Port "SITE_PORT" "3000"))
    Write-Host ("  App           http://localhost:{0}" -f (Get-Port "APP_PORT" "3001"))
    Write-Host ""
    Write-Host "The API and Nuxt apps hot-reload on file changes. First start compiles - give it a minute."
} else {
    Write-Host ""
    Write-Host "Next, on the host:"
    Write-Host "  dotnet watch --project backend/src/Api/DataBro.Api run"
    Write-Host "  pnpm --dir frontend dev:site"
}
