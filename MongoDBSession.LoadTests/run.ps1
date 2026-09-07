# MongoDBSession Docker Compose and NBomber Stress Testing Script

# Ensure script stops on first error
$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   MongoDBSession NBomber Stress Test" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Resolve repository root path
$RepoRoot = Resolve-Path "$PSScriptRoot\.."

# 1. Check Docker service status
Write-Host "[1/4] Checking Docker service status..." -ForegroundColor Yellow

$oldErrorAction = $ErrorActionPreference
$ErrorActionPreference = "SilentlyContinue"
& docker ps > $null 2>$null
$dockerExitCode = $LASTEXITCODE
$ErrorActionPreference = $oldErrorAction

if ($dockerExitCode -ne 0) {
    Write-Error "Docker is not running or not accessible. Please start Docker Desktop first!" 
    exit 1
}
Write-Host "-> Docker daemon is active." -ForegroundColor Green

# 2. Build and start API, Redis, and MongoDB containers
Write-Host "[2/4] Starting all services via Docker Compose (docker compose up -d --build)..." -ForegroundColor Yellow
try {
    Push-Location "$RepoRoot\MongoDBSession"
    & docker compose up -d --build
    $composeExit = $LASTEXITCODE
    Pop-Location
    if ($composeExit -ne 0) {
        throw "Failed to start docker compose services."
    }
    Write-Host "-> Docker Compose services are running in background." -ForegroundColor Green 
}
catch {
    Write-Error "Failed to start Docker Compose services!"
    exit 1
}

# 3. Wait for the API to be fully online and ready
Write-Host "[3/4] Waiting for API on port 8080 to be online and healthy..." -ForegroundColor Yellow
$retryCount = 0
$maxRetries = 30
$apiReady = $false

$oldErrorAction = $ErrorActionPreference
$ErrorActionPreference = "SilentlyContinue"

while (-not $apiReady -and $retryCount -lt $maxRetries) {
    try {
        $response = Invoke-RestMethod -Uri "http://127.0.0.1:8080/" -Method Get -TimeoutSec 15
        $apiReady = $true
    }
    catch {
        $retryCount++
        Write-Host "-> API is starting up or database is initializing... (Attempt $retryCount/$maxRetries)" -ForegroundColor Gray
        Start-Sleep -Seconds 3
    }
}

$ErrorActionPreference = $oldErrorAction

if (-not $apiReady) {
    Write-Error "Timeout: API on port 8080 did not become ready or initialize in time."       
    exit 1
}
Write-Host "-> API is online and fully healthy!" -ForegroundColor Green

# 4. Run NBomber Stress Tests in a loop
$running = $true
while ($running) {
    Write-Host "[4/4] Launching NBomber Stress Test scenario..." -ForegroundColor Yellow
    try {
        Push-Location $PSScriptRoot
        & dotnet run
        $stressExitCode = $LASTEXITCODE
        Pop-Location
    }
    catch {
        $stressExitCode = 1
    }

    if ($stressExitCode -eq 0) {
        Write-Host "==========================================" -ForegroundColor Green
        Write-Host "   Stress Testing Completed: SUCCESS!" -ForegroundColor Green
        Write-Host "==========================================" -ForegroundColor Green
    } else {
        Write-Host "==========================================" -ForegroundColor Red
        Write-Host "   Stress Testing Failed! (Exit Code: $stressExitCode)" -ForegroundColor Red  
        Write-Host "==========================================" -ForegroundColor Red
    }

    $inputChoice = Read-Host "Would you like to run another stress test scenario? (Y/N, default Y)"
    if ($inputChoice -and $inputChoice.Trim().ToUpper() -eq "N") {
        $running = $false
    }
    Write-Host ""
}

Write-Host "=================END=========================" -ForegroundColor Green
PAUSE
