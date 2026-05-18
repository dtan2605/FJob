param(
    [string]$ApiGatewayBaseUrl = "http://localhost:5100",
    [string]$JobCatalogBaseUrl = "http://localhost:5101",
    [string]$OrchestrationBaseUrl = "http://localhost:5102",
    [string]$JobSearchBaseUrl = "http://localhost:5103",
    [string]$IdentityBaseUrl = "http://localhost:5104",
    [string]$FrontendBaseUrl = "http://localhost:5105",
    [string]$AdminBaseUrl = "http://localhost:5106",
    [string]$AdminUsername = "",
    [string]$AdminPassword = ""
)

$failures = New-Object System.Collections.Generic.List[string]

function Test-JsonEndpoint {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Url,
        [string]$BearerToken = ""
    )

    try {
        $headers = @{}
        if ($BearerToken) {
            $headers["Authorization"] = "Bearer $BearerToken"
        }

        $response = Invoke-RestMethod -Uri $Url -Headers $headers -TimeoutSec 15
        Write-Host "[OK] $Name -> $Url"
        return $response
    }
    catch {
        $message = $_.Exception.Message
        $failures.Add("$Name failed: $message")
        Write-Host "[FAIL] $Name -> $Url :: $message" -ForegroundColor Red
        return $null
    }
}

Test-JsonEndpoint -Name "api-gateway health" -Url "$ApiGatewayBaseUrl/health" | Out-Null
Test-JsonEndpoint -Name "api-gateway ready" -Url "$ApiGatewayBaseUrl/ready" | Out-Null
Test-JsonEndpoint -Name "api-gateway metrics" -Url "$ApiGatewayBaseUrl/metrics" | Out-Null

Test-JsonEndpoint -Name "job-catalog health" -Url "$JobCatalogBaseUrl/health" | Out-Null
Test-JsonEndpoint -Name "job-catalog ready" -Url "$JobCatalogBaseUrl/ready" | Out-Null
Test-JsonEndpoint -Name "job-catalog metrics" -Url "$JobCatalogBaseUrl/metrics" | Out-Null

Test-JsonEndpoint -Name "orchestration health" -Url "$OrchestrationBaseUrl/health" | Out-Null
Test-JsonEndpoint -Name "orchestration ready" -Url "$OrchestrationBaseUrl/ready" | Out-Null
Test-JsonEndpoint -Name "orchestration metrics" -Url "$OrchestrationBaseUrl/metrics" | Out-Null

Test-JsonEndpoint -Name "job-search health" -Url "$JobSearchBaseUrl/health" | Out-Null
Test-JsonEndpoint -Name "job-search ready" -Url "$JobSearchBaseUrl/ready" | Out-Null
Test-JsonEndpoint -Name "job-search metrics" -Url "$JobSearchBaseUrl/metrics" | Out-Null

Test-JsonEndpoint -Name "identity health" -Url "$IdentityBaseUrl/health" | Out-Null
Test-JsonEndpoint -Name "identity ready" -Url "$IdentityBaseUrl/ready" | Out-Null
Test-JsonEndpoint -Name "identity metrics" -Url "$IdentityBaseUrl/metrics" | Out-Null

Test-JsonEndpoint -Name "frontend health" -Url "$FrontendBaseUrl/health" | Out-Null
Test-JsonEndpoint -Name "frontend ready" -Url "$FrontendBaseUrl/ready" | Out-Null
Test-JsonEndpoint -Name "frontend metrics" -Url "$FrontendBaseUrl/metrics" | Out-Null

$adminToken = ""
if ($AdminUsername -and $AdminPassword) {
    try {
        $loginResponse = Invoke-RestMethod `
            -Method Post `
            -Uri "$AdminBaseUrl/api/admin/auth/login" `
            -ContentType "application/json" `
            -Body (@{ username = $AdminUsername; password = $AdminPassword } | ConvertTo-Json) `
            -TimeoutSec 15

        $adminToken = $loginResponse.accessToken
        Write-Host "[OK] admin login -> $AdminBaseUrl/api/admin/auth/login"
    }
    catch {
        $message = $_.Exception.Message
        $failures.Add("admin login failed: $message")
        Write-Host "[FAIL] admin login -> $AdminBaseUrl/api/admin/auth/login :: $message" -ForegroundColor Red
    }
}

Test-JsonEndpoint -Name "admin health" -Url "$AdminBaseUrl/health" | Out-Null
Test-JsonEndpoint -Name "admin ready" -Url "$AdminBaseUrl/ready" | Out-Null

if ($adminToken) {
    Test-JsonEndpoint -Name "admin metrics" -Url "$AdminBaseUrl/metrics" -BearerToken $adminToken | Out-Null
    Test-JsonEndpoint -Name "admin dashboard" -Url "$AdminBaseUrl/api/admin/dashboard" -BearerToken $adminToken | Out-Null
}
else {
    Write-Host "[SKIP] admin metrics/dashboard need admin credentials."
}

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "Production readiness verification failed:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "- $_" -ForegroundColor Red }
    exit 1
}

Write-Host ""
Write-Host "Production readiness verification passed." -ForegroundColor Green
