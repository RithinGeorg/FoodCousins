Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Native {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][scriptblock]$Command
    )

    Write-Host "`n==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 10 SDK is required.' }
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw 'Node.js is required.' }
    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) { throw 'npm is required.' }

    $expectedSdk = '10.0.400'
    $actualSdk = (dotnet --version).Trim()
    if ($actualSdk -ne $expectedSdk) { throw "Expected .NET SDK $expectedSdk from global.json, but found $actualSdk." }

    Invoke-Native 'Restoring .NET' { dotnet restore FoodCousins.sln }
    Invoke-Native 'Building .NET' { dotnet build FoodCousins.sln -c Release --no-restore }
    Invoke-Native 'Running .NET tests' { dotnet test FoodCousins.sln -c Release --no-build }

    Push-Location 'frontend/foodcousins-web'
    try {
        if (Test-Path 'package-lock.json') {
            Invoke-Native 'Installing frontend dependencies (npm ci)' { npm ci --no-audit --no-fund }
        }
        else {
            Invoke-Native 'Installing frontend dependencies (npm install)' { npm install --no-audit --no-fund }
        }

        $env:VITE_API_BASE_URL = 'https://example.invalid'
        Invoke-Native 'Building React' { npm run build }
    }
    finally {
        Pop-Location
    }

    if (Get-Command terraform -ErrorAction SilentlyContinue) {
        Invoke-Native 'Initializing Terraform without backend' { terraform -chdir=infra/environments/dev init -backend=false }
        Invoke-Native 'Validating Terraform' { terraform -chdir=infra/environments/dev validate }
    }
    else {
        Write-Host '`nNOTE: Terraform is not installed; Terraform validation was skipped.' -ForegroundColor Yellow
    }

    Write-Host '`nSUCCESS: FoodCousins local build checks passed.' -ForegroundColor Green
}
catch {
    Write-Host '`nBUILD FAILED' -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}
