$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'This runner requires the Windows .NET Framework 4 C# compiler.'
}

$outputDir = Join-Path $repoRoot 'Temp/StandaloneQA'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$executable = Join-Path $outputDir 'EnemyActionScorerChecks.exe'
& $compiler /nologo /target:exe "/out:$executable" `
    (Join-Path $repoRoot 'Assets/Scripts/EnemyActionScorer.cs') `
    (Join-Path $PSScriptRoot 'EnemyActionScorerChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'EnemyActionScorer check compilation failed.' }

& $executable
if ($LASTEXITCODE -ne 0) { throw 'EnemyActionScorer checks failed.' }
