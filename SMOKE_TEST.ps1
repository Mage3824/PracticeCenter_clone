# Smoke-test: backend must be running on http://localhost:5001 (Development mode).
$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5001'
function Login([string]$user, [string]$password) {
    $body = @{ login = $user; password = $password } | ConvertTo-Json
    $response = Invoke-RestMethod -Method Post -Uri "$base/api/auth/login" -ContentType 'application/json' -Body $body
    if (-not $response.isSuccess -or -not $response.accessToken) { throw "Login failed for ${user}: $($response | ConvertTo-Json -Compress)" }
    return @{ Authorization = "Bearer $($response.accessToken)" }
}
Write-Host 'Checking employee login and history...'
$employee = Login 'employee' 'employee-password-1'
$date = Get-Date -Format 'yyyy-MM-dd'
$current = Invoke-RestMethod -Headers $employee -Uri "$base/api/work-days/current?date=$date"
$history = Invoke-RestMethod -Headers $employee -Uri "$base/api/work-days/history?from=$date&to=$date"
Write-Host "Current shift: $($current.id); history entries: $(@($history).Count)"
Write-Host 'Checking manager attendance summary...'
$manager = Login 'manager' 'manager-password-1'
$summary = Invoke-RestMethod -Headers $manager -Uri "$base/api/attendance/summary?date=$date"
if (@($summary).Count -lt 1) { throw 'Attendance summary returned no seeded users' }
$summary | Select-Object employeeName,status,workedMinutes,breakMinutes | Format-Table
Write-Host 'SMOKE TEST PASSED: login, history, attendance API' -ForegroundColor Green
