# WorkTime Vue launcher. Compatible with Windows PowerShell 5.1.
$ErrorActionPreference = 'Stop'
$frontendDirectory = Join-Path -Path $PSScriptRoot -ChildPath 'frontend'
if (-not (Test-Path -LiteralPath $frontendDirectory -PathType Container)) {
    throw "Missing frontend directory: $frontendDirectory"
}
Set-Location -LiteralPath $frontendDirectory
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Install Node.js 24 LTS: https://nodejs.org/'
}
if (-not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) {
    throw 'npm.cmd was not found. Check Node.js installation.'
}
Write-Host 'Node version:' (node --version)
if (-not (Test-Path -LiteralPath 'node_modules' -PathType Container)) {
    & npm.cmd ci
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed. Check internet access or package dependencies.' }
}
Write-Host 'Frontend: http://localhost:5173' -ForegroundColor Green
& npm.cmd run dev
if ($LASTEXITCODE -ne 0) { throw "Frontend exit code: $LASTEXITCODE" }
