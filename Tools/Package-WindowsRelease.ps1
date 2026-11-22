param(
    [string]$Version = 'v0.1.1',
    [string]$BuildDirectory
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^v\d+\.\d+\.\d+$') { throw 'Expected vMAJOR.MINOR.PATCH.' }
$project = Split-Path $PSScriptRoot -Parent
$output = Join-Path $project "Builds\$Version"
if (!$BuildDirectory) { $BuildDirectory = Join-Path $output 'Windows' }
$build = (Resolve-Path -LiteralPath $BuildDirectory).Path
foreach ($entry in @('BigOldGuy.exe', 'BigOldGuy_Data', 'MonoBleedingEdge', 'UnityPlayer.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $build $entry))) { throw "Missing build file: $entry" }
}
$base = "BigOldGuy-$Version-Windows-x64"
$zip = Join-Path $output "$base.zip"
$exe = Join-Path $output "$base.exe"
$manifest = Join-Path $output 'SHA256SUMS.txt'
foreach ($path in @($zip, $exe, $manifest)) {
    if (Test-Path -LiteralPath $path) { throw "Release artifact already exists: $path" }
}
New-Item -ItemType Directory -Force $output | Out-Null
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream = [IO.File]::Open($zip, [IO.FileMode]::CreateNew)
$archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $build -Recurse -File) {
        $name = $file.FullName.Substring($build.Length + 1).Replace('\', '/')
        if ($name -notmatch '(^|/)[^/]*BackUpThisFolder_ButDontShipItWithYourGame[^/]*/') {
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name)
        }
    }
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $project 'Assets/CardDuel/Licenses') -File) {
        if ($file.Extension -ne '.meta') {
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, "Licenses/$($file.Name)")
        }
    }
}
finally { $archive.Dispose(); $stream.Dispose() }
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe /platform:x64 /optimize+ /utf8output "/out:$exe" /reference:System.IO.Compression.dll /reference:System.Windows.Forms.dll "/resource:$zip,BigOldGuy.Payload" (Join-Path $PSScriptRoot 'WindowsLauncher.cs')
if ($LASTEXITCODE -ne 0) { throw 'Windows launcher compilation failed.' }
$hashLines = foreach ($path in @($exe, $zip)) {
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($path))"
}
$hashLines | Set-Content -LiteralPath $manifest -Encoding ascii
Get-Item -LiteralPath $exe, $zip, $manifest | Select-Object Name, Length
