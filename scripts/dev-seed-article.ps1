<#
.SYNOPSIS
    Publishes a demo article that exercises every content block type.

.DESCRIPTION
    Creates an Editor, then publishes one article containing all ten Phase 1 block types plus a
    deliberately unknown type, so the public site's renderer can be checked against real data
    rather than fixtures. Prints the resulting slug.

    Development convenience. Safe to re-run - each run uses a unique slug.

    Written for Windows PowerShell 5.1.

    -Count publishes several articles under one author, which is how to get a category past a
    single page of results.

.EXAMPLE
    ./scripts/dev-seed-article.ps1
    ./scripts/dev-seed-article.ps1 -Slug my-fixed-slug -Visibility premium
    ./scripts/dev-seed-article.ps1 -Count 25          # enough to paginate
#>
[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:5158",
    [string]$Slug,
    [ValidateSet("public", "premium")]
    [string]$Visibility = "public",
    [ValidateRange(1, 200)]
    [int]$Count = 1
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$stamp = Get-Date -Format "yyyyMMddHHmmss"
if (-not $Slug) { $Slug = "block-showcase-$stamp" }

# A random suffix as well as the timestamp: the stamp is second-precision, so consecutive runs
# would otherwise collide on the registration email.
$unique   = "$stamp-$([guid]::NewGuid().ToString('N').Substring(0, 6))"
$email    = "seed-$unique@databro.local"
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

# --- Taxonomy ---
# Idempotent-ish: slugs are unique (TX-1), so re-runs reuse the existing terms rather than failing.
function Get-OrCreateTerm {
    param([string]$Kind, [string]$Name, [string]$Slug, [string]$ParentId)

    $existing = Invoke-Api -Method GET -Path "/api/v1/$Kind"
    $match = $existing.data | Where-Object { $_.slug -eq $Slug } | Select-Object -First 1
    if ($match) { return $match.id }

    $body = @{ name = $Name; slug = $Slug }
    if ($ParentId) { $body.parentId = $ParentId }

    $created = Invoke-Api -Method POST -Path "/api/v1/authoring/$Kind" -Token $token -Body $body
    return $created.data.id
}

$aiId  = Get-OrCreateTerm -Kind "categories" -Name "Artificial Intelligence" -Slug "artificial-intelligence"
$llmId = Get-OrCreateTerm -Kind "categories" -Name "LLM Engineering" -Slug "llm-engineering" -ParentId $aiId

$tagIds = @(
    (Get-OrCreateTerm -Kind "tags" -Name "RAG" -Slug "rag")
    (Get-OrCreateTerm -Kind "tags" -Name "Embeddings" -Slug "embeddings")
    (Get-OrCreateTerm -Kind "tags" -Name "Python" -Slug "python")
)

Write-Host "  category: llm-engineering (child of artificial-intelligence)" -ForegroundColor DarkGray
Write-Host "  tags:     rag, embeddings, python" -ForegroundColor DarkGray

# Inline-content helpers (ADR-0009). Blocks now carry a node array rather than a plain string.
function Txt { param([string]$Text) @{ type = "text"; text = $Text } }
function Bold { param([string]$Text) @{ type = "text"; text = $Text; marks = @(@{ type = "bold" }) } }
function Mono { param([string]$Text) @{ type = "text"; text = $Text; marks = @(@{ type = "code" }) } }
function Link { param([string]$Text, [string]$Href) @{ type = "text"; text = $Text; marks = @(@{ type = "link"; attrs = @{ href = $Href } }) } }
function MathI { param([string]$Latex) @{ type = "mathInline"; attrs = @{ latex = $Latex } } }

$blocks = @(
    @{ id = "b01"; type = "heading";   data = @{ level = 2; text = "What Retrieval-Augmented Generation Actually Solves" } }
    @{ id = "b02"; type = "paragraph"; data = @{ content = @(
        (Txt "RAG grounds a language model in documents you control, so answers cite your data instead of the model's recollection. See the "),
        (Link "pgvector documentation" "https://github.com/pgvector/pgvector"),
        (Txt " for the storage side, or start with "),
        (Mono "sentence-transformers"),
        (Txt ".")
    ) } }
    @{ id = "b03"; type = "callout";   data = @{ variant = "tip"; content = @(
        (Txt "Start with good chunking. Most bad RAG results are "),
        (Bold "retrieval"),
        (Txt " problems, not model problems.")
    ) } }
    @{ id = "b04"; type = "heading";   data = @{ level = 3; text = "A Minimal Pipeline" } }
    @{ id = "b05"; type = "code";      data = @{ language = "python"; filename = "rag.py"
        code   = "chunks = split(document, size=512)`nindex.upsert(embed(chunks))`nhits = index.query(embed(question), k=5)"
        output = "Retrieved 5 chunks in 12ms" } }
    # Steps carrying their own code sample - the nested-blocks capability from ADR-0009.
    @{ id = "b06"; type = "list";      data = @{ ordered = $true; items = @(
        @{ content = @((Txt "Chunk the source documents")); blocks = @(
            @{ id = "b06a"; type = "code"; data = @{ language = "python"; code = "chunks = split(document, size=512, overlap=64)" } }
        ) },
        @{ content = @((Txt "Embed and index the chunks")) },
        @{ content = @((Txt "Retrieve the top-"), (Mono "k"), (Txt " for a question")) },
        @{ content = @((Txt "Ground the generation in what you retrieved")) }
    ) } }
    @{ id = "b07"; type = "quote";     data = @{ content = @((Txt "The model is only as good as what you put in front of it.")); attribution = "Every RAG postmortem, eventually" } }
    @{ id = "b08"; type = "table";     data = @{
        headers = @(@((Txt "Strategy")), @((Txt "Recall")), @((Txt "Cost")))
        rows = @(
            @(@((Mono "BM25")),        @((Txt "Low")),     @((Txt "Low"))),
            @(@((Txt "Dense vector")), @((Txt "High")),    @((Txt "Medium"))),
            @(@((Txt "Hybrid")),       @((Txt "Highest")), @((Txt "High")))
        ) } }
    # Math moved into Phase 1 with ADR-0009: unavoidable for ML explanation.
    @{ id = "b09"; type = "paragraph"; data = @{ content = @(
        (Txt "Attention cost grows as "), (MathI "O(n^2)"), (Txt " in sequence length, which is why retrieval beats a longer context window:")
    ) } }
    @{ id = "b10"; type = "math";      data = @{ latex = "\text{Attention}(Q,K,V) = \text{softmax}\!\left(\frac{QK^{T}}{\sqrt{d_k}}\right)V" } }
    @{ id = "b11"; type = "image";     data = @{ mediaId = "00000000-0000-0000-0000-000000000001"; alt = "Diagram of a retrieval-augmented generation pipeline"; caption = "The retrieval step is where most quality is won or lost." } }
    @{ id = "b12"; type = "divider";   data = @{} }
    @{ id = "b13"; type = "embed";     data = @{ provider = "youtube"; url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" } }
    @{ id = "b14"; type = "embed";     data = @{ provider = "unknown"; url = "https://example.com/not-allowlisted" } }
    # Deliberately unrenderable: proves the site degrades instead of throwing.
    @{ id = "b15"; type = "chart";     data = @{ series = @(1, 2, 3) } }
    # A hostile link, to prove the anchor is dropped while the prose survives.
    @{ id = "b16"; type = "paragraph"; data = @{ content = @(
        (Txt "Retrieval quality compounds. "),
        (Link "This link is hostile and must not become an anchor" "javascript:alert(1)")
    ) } }
)

$slugs = @()

for ($i = 1; $i -le $Count; $i++) {
    $articleSlug = if ($Count -eq 1) { $Slug } else { "$Slug-$i" }
    $suffix = if ($Count -eq 1) { "" } else { " ($i)" }

    $create = Invoke-Api -Method POST -Path "/api/v1/authoring/articles" -Token $token -Body @{
        title      = "Retrieval-Augmented Generation, End to End$suffix"
        summary    = "A practical walkthrough of RAG: chunking, embedding, retrieval, and grounding - and where each step usually goes wrong."
        slug       = $articleSlug
        visibility = $Visibility
        categoryId = $llmId
        tagIds     = $tagIds
        content    = @{ version = 1; blocks = $blocks }
        seo        = @{
            metaTitle       = "Retrieval-Augmented Generation, End to End | DataBro"
            metaDescription = "How RAG actually works in production: chunking strategy, embeddings, hybrid retrieval, and grounding a model in documents you control."
            robots          = "index,follow"
        }
    }

    $id = $create.data.id
    Invoke-Api -Method POST -Path "/api/v1/authoring/articles/$id/publish" -Token $token | Out-Null
    $slugs += $articleSlug
}

Write-Host ""
Write-Host "Published $Count article(s)." -ForegroundColor Green
Write-Host "  author:   Ada Lovelace"
Write-Host "  category: http://localhost:3000/categories/llm-engineering"
Write-Host "  first:    http://localhost:3000/articles/$($slugs[0])"
