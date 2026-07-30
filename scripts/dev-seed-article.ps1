<#
.SYNOPSIS
    Publishes a demo article that exercises every content block type.

.DESCRIPTION
    Creates an Editor, then publishes one article containing all ten Phase 1 block types plus a
    deliberately unknown type, so the public site's renderer can be checked against real data
    rather than fixtures. Prints the resulting slug.

    Development convenience. Safe to re-run - each run uses a unique slug.

    Written for Windows PowerShell 5.1.

.EXAMPLE
    ./scripts/dev-seed-article.ps1
    ./scripts/dev-seed-article.ps1 -Slug my-fixed-slug -Visibility premium
#>
[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:5158",
    [string]$Slug,
    [ValidateSet("public", "premium")]
    [string]$Visibility = "public"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$stamp = Get-Date -Format "yyyyMMddHHmmss"
if (-not $Slug) { $Slug = "block-showcase-$stamp" }
$email    = "seed-$stamp@databro.local"
$password = "Se3d-Test-Pw!"

function Invoke-Api {
    param([string]$Method, [string]$Path, $Body, [string]$Token)

    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }

    $params = @{
        Method          = $Method
        Uri             = "$ApiBaseUrl$Path"
        Headers         = $headers
        ContentType     = "application/json"
        UseBasicParsing = $true
    }
    if ($null -ne $Body) { $params["Body"] = ($Body | ConvertTo-Json -Depth 15) }

    try {
        return (Invoke-WebRequest @params).Content | ConvertFrom-Json
    }
    catch [System.Net.WebException] {
        $webResponse = $_.Exception.Response
        if ($null -eq $webResponse) { throw }
        $reader = New-Object System.IO.StreamReader($webResponse.GetResponseStream())
        $content = $reader.ReadToEnd()
        $reader.Close()
        throw "$Method $Path failed: $content"
    }
}

Write-Host "Seeding a block-showcase article on $ApiBaseUrl" -ForegroundColor Cyan

Invoke-Api -Method POST -Path "/api/v1/auth/register" -Body @{
    email = $email; password = $password; displayName = "Ada Lovelace"
} | Out-Null

& (Join-Path $PSScriptRoot "dev-grant-role.ps1") -Email $email -Role Editor | Out-Null

$login = Invoke-Api -Method POST -Path "/api/v1/auth/login" -Body @{ email = $email; password = $password }
$token = $login.data.accessToken

$blocks = @(
    @{ id = "b01"; type = "heading";   data = @{ level = 2; text = "What Retrieval-Augmented Generation Actually Solves" } }
    @{ id = "b02"; type = "paragraph"; data = @{ text = "RAG grounds a language model in documents you control, so answers cite your data instead of the model's recollection." } }
    @{ id = "b03"; type = "callout";   data = @{ variant = "tip"; text = "Start with good chunking. Most bad RAG results are retrieval problems, not model problems." } }
    @{ id = "b04"; type = "heading";   data = @{ level = 3; text = "A Minimal Pipeline" } }
    @{ id = "b05"; type = "code";      data = @{ language = "python"; filename = "rag.py"; code = "chunks = split(document, size=512)`nindex.upsert(embed(chunks))`nhits = index.query(embed(question), k=5)" } }
    @{ id = "b06"; type = "list";      data = @{ ordered = $true; items = @("Chunk the source documents", "Embed and index the chunks", "Retrieve the top-k for a question", "Ground the generation in what you retrieved") } }
    @{ id = "b07"; type = "quote";     data = @{ text = "The model is only as good as what you put in front of it."; attribution = "Every RAG postmortem, eventually" } }
    @{ id = "b08"; type = "table";     data = @{ headers = @("Strategy", "Recall", "Cost"); rows = @(@("Keyword", "Low", "Low"), @("Dense vector", "High", "Medium"), @("Hybrid", "Highest", "High")) } }
    @{ id = "b09"; type = "image";     data = @{ mediaId = "00000000-0000-0000-0000-000000000001"; alt = "Diagram of a retrieval-augmented generation pipeline"; caption = "The retrieval step is where most quality is won or lost." } }
    @{ id = "b10"; type = "divider";   data = @{} }
    @{ id = "b11"; type = "embed";     data = @{ provider = "youtube"; url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" } }
    @{ id = "b12"; type = "embed";     data = @{ provider = "unknown"; url = "https://example.com/not-allowlisted" } }
    # Deliberately unrenderable: proves the site degrades instead of throwing.
    @{ id = "b13"; type = "chart";     data = @{ series = @(1, 2, 3) } }
    @{ id = "b14"; type = "paragraph"; data = @{ text = "Retrieval quality compounds: better chunks make better context, and better context makes shorter prompts." } }
)

$create = Invoke-Api -Method POST -Path "/api/v1/authoring/articles" -Token $token -Body @{
    title      = "Retrieval-Augmented Generation, End to End"
    summary    = "A practical walkthrough of RAG: chunking, embedding, retrieval, and grounding - and where each step usually goes wrong."
    slug       = $Slug
    visibility = $Visibility
    content    = @{ version = 1; blocks = $blocks }
    seo        = @{
        metaTitle       = "Retrieval-Augmented Generation, End to End | DataBro"
        metaDescription = "How RAG actually works in production: chunking strategy, embeddings, hybrid retrieval, and grounding a model in documents you control."
        robots          = "index,follow"
    }
}

$id = $create.data.id
Invoke-Api -Method POST -Path "/api/v1/authoring/articles/$id/publish" -Token $token | Out-Null

Write-Host ""
Write-Host "Published." -ForegroundColor Green
Write-Host "  slug:   $Slug"
Write-Host "  author: Ada Lovelace"
Write-Host "  api:    $ApiBaseUrl/api/v1/articles/$Slug"
Write-Host "  site:   http://localhost:3000/articles/$Slug"
