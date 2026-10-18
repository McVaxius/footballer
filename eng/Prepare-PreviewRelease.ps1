param(
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [Parameter(Mandatory)][string]$PreviewDirectory,
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$Tag
)
$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $ReleaseDirectory 'footballer.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest -is [array]) { throw 'Expected a single release manifest.' }
if ($Tag -ne "v$($manifest.AssemblyVersion)") {
    throw 'Release tag must match the packaged plugin version.'
}
$images = @(Get-ChildItem -LiteralPath $PreviewDirectory -Filter '*.png' | Sort-Object Name)
if ($images.Count -lt 1 -or $images.Count -gt 5) { throw 'Expected one to five validated preview PNGs.' }
$baseUrl = "https://github.com/$Repository/releases/download/$Tag"
$imageUrls = @($images | ForEach-Object { "$baseUrl/$($_.Name)" })
$repositoryRoot = Split-Path $PSScriptRoot -Parent
foreach ($sourcePath in @('repo.json', 'footballer/footballer.json')) {
    $source = Get-Content -LiteralPath (Join-Path $repositoryRoot $sourcePath) -Raw | ConvertFrom-Json
    if ($source -is [array]) { $source = $source[0] }
    if ($source.AssemblyVersion -ne $manifest.AssemblyVersion -or
        (ConvertTo-Json -InputObject @($source.ImageUrls) -Compress) -ne (ConvertTo-Json -InputObject $imageUrls -Compress)) {
        throw "$sourcePath must contain this release's version and ImageUrls. Update the existing manifests before releasing."
    }
}
$manifest | Add-Member NoteProperty ImageUrls $imageUrls -Force
foreach ($key in @('DownloadLinkInstall', 'DownloadLinkUpdate')) {
    $manifest | Add-Member NoteProperty $key "$baseUrl/latest.zip" -Force
}
$manifest | Add-Member NoteProperty IconUrl "https://raw.githubusercontent.com/$Repository/main/footballer/images/icon.png" -Force
$json = ConvertTo-Json -InputObject $manifest -Depth 30
[IO.File]::WriteAllText($manifestPath, $json)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open((Join-Path $ReleaseDirectory 'latest.zip'), 'Update')
try {
    $entries = @($archive.Entries | Where-Object { $_.FullName -eq 'footballer.json' })
    if ($entries.Count -ne 1) { throw 'Expected exactly one packaged footballer.json.' }
    $entries[0].Delete()
    $entry = $archive.CreateEntry('footballer.json')
    $writer = [IO.StreamWriter]::new($entry.Open(), [Text.UTF8Encoding]::new($false))
    try { $writer.Write($json) } finally { $writer.Dispose() }
} finally { $archive.Dispose() }
Copy-Item -LiteralPath $images.FullName -Destination $ReleaseDirectory
Write-Output "Prepared $Tag, plugin $($manifest.AssemblyVersion), $($images.Count) preview(s)."
