<#
.SYNOPSIS
    End-to-end smoke test of the running DataBro API.

.DESCRIPTION
    Exercises the full Phase 1 slice against a live API + Postgres:
      register -> grant Editor -> login -> create draft -> 404 while unpublished
      -> publish -> public read by slug -> unpublish -> 404 again.

    Complements `dotnet test` (which uses throwaway Testcontainers): this verifies the
    stack you are actually running. Safe to re-run - each run uses a unique user and slug.

    Written for Windows PowerShell 5.1 (no PS7-only parameters).

.EXAMPLE
    ./scripts/dev-smoke.ps1
    ./scripts/dev-smoke.ps1 -ApiBaseUrl http://localhost:5158
#>
[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:5158"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$stamp    = Get-Date -Format "yyyyMMddHHmmss"
$email    = "smoke-$stamp@databro.local"
$password = "Sm0ke-Test-Pw!"
$slug     = "smoke-test-$stamp"
$step     = 0

function Step([string]$message) {
    $script:step++
    Write-Host ("[{0}] {1}" -f $script:step, $message) -ForegroundColor Cyan
}

function Ok([string]$message) { Write-Host "    OK  $message" -ForegroundColor Green }

# Invoke-WebRequest throws on any non-2xx in PS 5.1, so assert the status inside the catch
# rather than relying on -SkipHttpErrorCheck (PS 7+).
function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        $Body,
        [string]$Token,
        [int[]]$ExpectStatus = @(200)
    )

    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }

    $params = @{
        Method          = $Method
        Uri             = "$ApiBaseUrl$Path"
        Headers         = $headers
        ContentType     = "application/json"
        UseBasicParsing = $true
    }
    if ($null -ne $Body) { $params["Body"] = ($Body | ConvertTo-Json -Depth 12) }

    $status  = 0
    $content = ""
    try {
        $response = Invoke-WebRequest @params
        $status   = [int]$response.StatusCode
        $content  = $response.Content
    }
    catch [System.Net.WebException] {
        $webResponse = $_.Exception.Response
        if ($null -eq $webResponse) { throw }
        $status = [int]$webResponse.StatusCode
        $reader = New-Object System.IO.StreamReader($webResponse.GetResponseStream())
        $content = $reader.ReadToEnd()
        $reader.Close()
    }

    if ($ExpectStatus -notcontains $status) {
        throw "$Method $Path -> HTTP $status (expected $($ExpectStatus -join '/')). Body: $content"
    }
    if ([string]::IsNullOrWhiteSpace($content)) { return $null }
    return ($content | ConvertFrom-Json)
}

Write-Host "Smoke-testing $ApiBaseUrl" -ForegroundColor White
Write-Host ""

Step "Health"
$health = Invoke-Api -Method GET -Path "/health"
if ($health.status -ne "healthy") { throw "Unexpected health payload: $($health | ConvertTo-Json -Compress)" }
Ok "API is healthy"

Step "Register $email"
Invoke-Api -Method POST -Path "/api/v1/auth/register" -Body @{
    email = $email; password = $password; displayName = "Smoke Test"
} | Out-Null
Ok "registered (default role: Reader)"

Step "Grant Editor (publishing is a distinct permission from authoring)"
& (Join-Path $PSScriptRoot "dev-grant-role.ps1") -Email $email -Role Editor | Out-Null
Ok "granted"

Step "Log in"
$login = Invoke-Api -Method POST -Path "/api/v1/auth/login" -Body @{ email = $email; password = $password }
$token = $login.data.accessToken
if (-not $token) { throw "No access token returned." }
Ok "access token issued"

Step "Anonymous authoring request is rejected"
Invoke-Api -Method POST -Path "/api/v1/authoring/articles" -ExpectStatus 401 -Body @{
    title = "nope"; summary = "nope"; content = @{ version = 1; blocks = @() }
} | Out-Null
Ok "401 without a token"

Step "Create draft"
$create = Invoke-Api -Method POST -Path "/api/v1/authoring/articles" -Token $token -Body @{
    title   = "Smoke Test Article $stamp"
    summary = "Created by scripts/dev-smoke.ps1 to verify the local stack end to end."
    slug    = $slug
    content = @{
        version = 1
        blocks  = @(
            @{ id = "b1"; type = "paragraph"; data = @{ text = "Hello from the smoke test." } }
        )
    }
}
$articleId = $create.data.id
if (-not $articleId) { throw "No article id returned." }
Ok "draft $articleId"

Step "Draft is not publicly readable"
Invoke-Api -Method GET -Path "/api/v1/articles/$slug" -ExpectStatus 404 | Out-Null
Ok "404 while unpublished"

Step "Publish"
Invoke-Api -Method POST -Path "/api/v1/authoring/articles/$articleId/publish" -Token $token | Out-Null
Ok "published"

Step "Public read by slug"
$public = Invoke-Api -Method GET -Path "/api/v1/articles/$slug"
if ($public.data.slug -ne $slug) { throw "Slug mismatch: got '$($public.data.slug)'." }
if (-not $public.data.publishedAt) { throw "publishedAt was not set." }
Ok "served: '$($public.data.title)' (v$($public.data.currentVersion))"

Step "Unpublish"
Invoke-Api -Method POST -Path "/api/v1/authoring/articles/$articleId/unpublish" -Token $token | Out-Null
Invoke-Api -Method GET -Path "/api/v1/articles/$slug" -ExpectStatus 404 | Out-Null
Ok "hidden from the public surface again"

Write-Host ""
Write-Host "Smoke test passed - $script:step steps." -ForegroundColor Green
