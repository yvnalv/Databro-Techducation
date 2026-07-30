<#
.SYNOPSIS
    Grants an RBAC role to a local development user.

.DESCRIPTION
    Self-registration only ever assigns `Reader` (Roles.Default). To exercise the authoring
    endpoints locally you need Author/Editor/Admin, so this writes straight into the Identity
    schema of the local Postgres container.

    Development convenience only - there is deliberately no API surface for self-promotion.
    The user must log in again afterwards: permissions are baked into the JWT at issue time.

    Written for Windows PowerShell 5.1. The SQL is piped over stdin rather than passed via
    `psql -c`, because 5.1 strips the double quotes that ASP.NET Identity's PascalCase table
    names require.

.EXAMPLE
    ./scripts/dev-grant-role.ps1 -Email author@databro.local -Role Editor
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Email,
    [ValidateSet("Reader", "Author", "Editor", "Admin")]
    [string]$Role = "Admin"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

# Credentials come from .env so this keeps working if they are changed.
$envPath = Join-Path $root ".env"
$dbUser = "databro"
$dbName = "databro"
if (Test-Path $envPath) {
    Get-Content $envPath | ForEach-Object {
        if ($_ -match "^\s*POSTGRES_USER\s*=\s*(.+?)\s*$") { $dbUser = $Matches[1] }
        if ($_ -match "^\s*POSTGRES_DB\s*=\s*(.+?)\s*$")   { $dbName = $Matches[1] }
    }
}

$escapedEmail = $Email.Replace("'", "''")

$sql = @"
INSERT INTO identity."AspNetUserRoles" (user_id, role_id)
SELECT u.id, r.id
FROM identity."AspNetUsers" u
JOIN identity."AspNetRoles" r ON r.normalized_name = UPPER('$Role')
WHERE u.normalized_email = UPPER('$escapedEmail')
ON CONFLICT DO NOTHING;
"@

$output = $sql | docker compose exec -T postgres psql -U $dbUser -d $dbName -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) { throw "Role grant failed. Is the postgres container up?" }

if ($output -match "INSERT 0 0") {
    Write-Host "No change: '$Email' either does not exist or already has '$Role'." -ForegroundColor Yellow
} else {
    Write-Host "Granted '$Role' to '$Email'. Log in again to pick up the new permission claims." -ForegroundColor Green
}
