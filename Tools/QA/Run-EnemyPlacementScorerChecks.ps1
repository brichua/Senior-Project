$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'This runner requires the Windows .NET Framework 4 C# compiler.'
}
$outputDir = Join-Path $repoRoot 'Temp/StandaloneQA'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$executable = Join-Path $outputDir 'EnemyPlacementScorerChecks.exe'
& $compiler /nologo /target:exe "/out:$executable" `
    (Join-Path $repoRoot 'Assets/Scripts/EnemyActionScorer.cs') `
    (Join-Path $repoRoot 'Assets/Scripts/EnemyPlacementScorer.cs') `
    (Join-Path $PSScriptRoot 'EnemyPlacementScorerChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Enemy placement check compilation failed.' }
& $executable
if ($LASTEXITCODE -ne 0) { throw 'Enemy placement checks failed.' }
