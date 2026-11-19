# WorkTime API launcher. Compatible with Windows PowerShell 5.1.
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0'
}

$projectFile = Join-Path -Path $PSScriptRoot -ChildPath 'backend\src\WorkTime.Api\WorkTime.Api.csproj'
if (-not (Test-Path -LiteralPath $projectFile -PathType Leaf)) {
    throw "Missing backend project file: $projectFile. Check that you extracted the whole archive."
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
if ([string]::IsNullOrWhiteSpace($env:JwtSettings__SecretKey)) {
    $bytes = New-Object 'byte[]' 48
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) }
    finally { $rng.Dispose() }
    $env:JwtSettings__SecretKey = [Convert]::ToBase64String($bytes)
}

Write-Host 'Starting WorkTime API on http://localhost:5001' -ForegroundColor Green
Write-Host 'Swagger: http://localhost:5001/swagger' -ForegroundColor Green
Write-Host 'Development mode uses temporary InMemory data.' -ForegroundColor Yellow
& dotnet run --project $projectFile --no-launch-profile --urls 'http://localhost:5001'
if ($LASTEXITCODE -ne 0) { throw "Backend exit code: $LASTEXITCODE" }
