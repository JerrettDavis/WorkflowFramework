param(
    [string]$ArtifactRoot = (Join-Path $PSScriptRoot "..\TestResults\dashboard-ui\latest")
)

$ErrorActionPreference = "Stop"

$artifactRootPath = [System.IO.Path]::GetFullPath($ArtifactRoot)
$scenarioRoot = Join-Path $artifactRootPath "scenarios"
$livingDocRoot = Join-Path $artifactRootPath "livingdoc"
$allureRoot = Join-Path $artifactRootPath "allure-results"

if (-not (Test-Path $scenarioRoot)) {
    throw "Scenario artifact root not found: $scenarioRoot"
}

New-Item -ItemType Directory -Path $livingDocRoot -Force | Out-Null
New-Item -ItemType Directory -Path $allureRoot -Force | Out-Null

function Get-RelativeArtifactPath {
    param(
        [string]$FromPath,
        [string]$ToPath
    )

    $fromUri = [Uri]::new(([System.IO.Path]::GetFullPath($FromPath.TrimEnd('\')) + '\'))
    $toUri = [Uri]::new([System.IO.Path]::GetFullPath($ToPath))
    return [Uri]::UnescapeDataString($fromUri.MakeRelativeUri($toUri).ToString()).Replace('/', '\')
}

function HtmlEncode {
    param([string]$Value)
    return [System.Net.WebUtility]::HtmlEncode($Value)
}

function Get-HrefPath {
    param([string]$RelativePath)
    return [Uri]::EscapeUriString($RelativePath.Replace('\', '/'))
}

$manifests = Get-ChildItem -Path $scenarioRoot -Recurse -Filter manifest.json |
    Sort-Object FullName |
    ForEach-Object {
        $json = Get-Content $_.FullName -Raw | ConvertFrom-Json
        [pscustomobject]@{
            Title = $json.title
            Passed = [bool]$json.passed
            Tags = @($json.tags)
            ManifestPath = $_.FullName
            ScenarioDirectory = $_.Directory.FullName
            Artifacts = @($json.artifacts | ForEach-Object {
                [pscustomobject]@{
                    Kind = $_.kind
                    Path = $_.path
                    PromotedPath = $_.promotedPath
                }
            })
        }
    }

$summaryPath = Join-Path $allureRoot "dashboard-summary.json"
$summaryPayload = [pscustomobject]@{
    generatedAt = [DateTimeOffset]::UtcNow.ToString("O")
    artifactRoot = $artifactRootPath
    scenarioCount = $manifests.Count
    scenarios = $manifests
}
$summaryPayload | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryPath -Encoding utf8

$scenarioCards = foreach ($manifest in $manifests) {
    $statusClass = if ($manifest.Passed) { "passed" } else { "failed" }
    $statusText = if ($manifest.Passed) { "Passed" } else { "Failed" }
    $tagHtml = if ($manifest.Tags.Count -gt 0) {
        ($manifest.Tags | ForEach-Object { "<span class='tag'>$(HtmlEncode([string]$_))</span>" }) -join ""
    }
    else {
        "<span class='tag'>untagged</span>"
    }

    $artifactHtml = if ($manifest.Artifacts.Count -gt 0) {
        "<ul>" + (($manifest.Artifacts | ForEach-Object {
            $artifactPath = Get-RelativeArtifactPath -FromPath $livingDocRoot -ToPath $_.Path
            $artifactHref = Get-HrefPath -RelativePath $artifactPath
            $promoted = if ([string]::IsNullOrWhiteSpace($_.PromotedPath)) {
                ""
            }
            else {
                $promotedRelative = Get-RelativeArtifactPath -FromPath $livingDocRoot -ToPath $_.PromotedPath
                $promotedHref = Get-HrefPath -RelativePath $promotedRelative
                " <a href='$promotedHref'>(docs-promotable)</a>"
            }
            "<li><strong>$(HtmlEncode([string]$_.Kind))</strong>: <a href='$artifactHref'>$(HtmlEncode([System.IO.Path]::GetFileName($_.Path)))</a>$promoted</li>"
        }) -join "") + "</ul>"
    }
    else {
        "<p class='empty'>No artifacts captured.</p>"
    }

    $manifestRelative = Get-RelativeArtifactPath -FromPath $livingDocRoot -ToPath $manifest.ManifestPath

@"
<article class='scenario'>
  <header>
    <h2>$(HtmlEncode([string]$manifest.Title))</h2>
    <span class='status $statusClass'>$statusText</span>
  </header>
  <div class='tags'>$tagHtml</div>
  <p class='meta'>Manifest: $(HtmlEncode([string]$manifestRelative))</p>
  $artifactHtml
</article>
"@
}

$html = @"
<!DOCTYPE html>
<html lang='en'>
<head>
  <meta charset='utf-8' />
  <title>WorkflowFramework Dashboard UI LivingDoc</title>
  <style>
    body { font-family: Inter, Segoe UI, Arial, sans-serif; background: #0f172a; color: #e2e8f0; margin: 0; padding: 32px; }
    h1 { margin-top: 0; }
    a { color: #93c5fd; }
    .summary { color: #94a3b8; margin-bottom: 24px; }
    .scenario { background: #111827; border: 1px solid #1f2937; border-radius: 16px; padding: 20px; margin-bottom: 16px; }
    .scenario header { display: flex; align-items: center; justify-content: space-between; gap: 16px; }
    .status { border-radius: 999px; padding: 4px 10px; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.08em; }
    .status.passed { background: rgba(34,197,94,0.15); color: #86efac; }
    .status.failed { background: rgba(239,68,68,0.15); color: #fca5a5; }
    .tags { margin: 10px 0; display: flex; flex-wrap: wrap; gap: 8px; }
    .tag { background: #1e293b; color: #cbd5e1; border-radius: 999px; padding: 4px 10px; font-size: 12px; }
    .meta, .empty { color: #94a3b8; font-size: 14px; }
    ul { margin: 12px 0 0 18px; }
    li { margin-bottom: 6px; }
  </style>
</head>
<body>
  <h1>WorkflowFramework Dashboard UI LivingDoc</h1>
  <p class='summary'>Generated $(Get-Date -Format "u") from $($manifests.Count) scenario manifest(s). Raw summary JSON is available at <a href='../allure-results/dashboard-summary.json'>../allure-results/dashboard-summary.json</a>.</p>
  $($scenarioCards -join "`n")
</body>
</html>
"@

$indexPath = Join-Path $livingDocRoot "index.html"
Set-Content -Path $indexPath -Value $html -Encoding utf8

Write-Host "Generated living doc: $indexPath"
Write-Host "Generated summary JSON: $summaryPath"
