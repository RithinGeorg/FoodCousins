param(
    [Parameter(Mandatory=$true)][string]$GitHubOwner,
    [Parameter(Mandatory=$true)][string]$GitHubRepository,
    [Parameter(Mandatory=$true)][string]$AlertEmail,
    [string]$Location = 'australiaeast',
    [string]$SqlAdminPassword = '',
    [string]$JwtSigningKey = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Az {
    param([Parameter(Mandatory=$true)][scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Azure CLI command failed with exit code $LASTEXITCODE." }
}

function Invoke-Gh {
    param([Parameter(Mandatory=$true)][scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "GitHub CLI command failed with exit code $LASTEXITCODE." }
}


function Ensure-RoleAssignment {
    param(
        [Parameter(Mandatory=$true)][string]$PrincipalId,
        [Parameter(Mandatory=$true)][string]$Role,
        [Parameter(Mandatory=$true)][string]$Scope
    )

    $existing = az role assignment list --assignee-object-id $PrincipalId --role $Role --scope $Scope --query "[0].id" -o tsv 2>$null
    $global:LASTEXITCODE = 0
    if ([string]::IsNullOrWhiteSpace($existing)) {
        Invoke-Az {
            az role assignment create `
                --assignee-object-id $PrincipalId `
                --assignee-principal-type ServicePrincipal `
                --role $Role `
                --scope $Scope `
                --output none
        }
    }
}

function New-SafeRandomString([int]$Bytes = 32) {
    $buffer = New-Object byte[] $Bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($buffer)
    return [Convert]::ToBase64String($buffer).Replace('+','A').Replace('/','B').Replace('=','')
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) { throw 'Azure CLI (az) is required.' }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI (gh) is required.' }

Write-Host '==> Verifying Azure login' -ForegroundColor Cyan
$accountJson = az account show -o json
if ($LASTEXITCODE -ne 0) { throw "Run 'az login' first." }
$account = $accountJson | ConvertFrom-Json
$subscriptionId = [string]$account.id
$tenantId = [string]$account.tenantId

Write-Host '==> Verifying GitHub login and repository' -ForegroundColor Cyan
gh auth status | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Run 'gh auth login' first." }
$repoJson = gh api "repos/$GitHubOwner/$GitHubRepository"
if ($LASTEXITCODE -ne 0) { throw "GitHub repository $GitHubOwner/$GitHubRepository was not found or is not accessible." }
$repo = $repoJson | ConvertFrom-Json
$repoId = [string]$repo.id
$ownerId = [string]$repo.owner.id

# Repositories created after July 15, 2026 use GitHub's immutable OIDC subject by default.
$oidcSubject = "repo:${GitHubOwner}@${ownerId}/${GitHubRepository}@${repoId}:environment:dev"

if ([string]::IsNullOrWhiteSpace($SqlAdminPassword)) {
    $SqlAdminPassword = 'Fc!' + (New-SafeRandomString 24)
}
if ([string]::IsNullOrWhiteSpace($JwtSigningKey)) {
    $JwtSigningKey = New-SafeRandomString 48
}
if ($SqlAdminPassword.Length -lt 16) { throw 'SQL admin password must be at least 16 characters.' }
if ($JwtSigningKey.Length -lt 32) { throw 'JWT signing key must be at least 32 characters.' }

$suffix = Get-Random -Minimum 100000 -Maximum 999999
$stateRg = 'rg-foodcousins-tfstate'
$stateAccount = "stfctf$suffix"
$container = 'tfstate'
$key = 'dev.terraform.tfstate'
$identityName = 'id-foodcousins-github-dev'

Write-Host '==> Creating Terraform state resources' -ForegroundColor Cyan
Invoke-Az { az group create --name $stateRg --location $Location --output none }
Invoke-Az { az storage account create --name $stateAccount --resource-group $stateRg --location $Location --sku Standard_LRS --kind StorageV2 --min-tls-version TLS1_2 --allow-blob-public-access false --output none }
Invoke-Az { az storage container create --name $container --account-name $stateAccount --auth-mode key --output none }

Write-Host '==> Creating GitHub deployment managed identity' -ForegroundColor Cyan
$identityExists = az identity show --name $identityName --resource-group $stateRg --query id -o tsv 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($identityExists)) {
    $global:LASTEXITCODE = 0
    Invoke-Az { az identity create --name $identityName --resource-group $stateRg --location $Location --output none }
}

$clientId = az identity show --name $identityName --resource-group $stateRg --query clientId -o tsv
$principalId = az identity show --name $identityName --resource-group $stateRg --query principalId -o tsv
if ([string]::IsNullOrWhiteSpace($clientId) -or [string]::IsNullOrWhiteSpace($principalId)) { throw 'Could not read managed identity IDs.' }

Write-Host '==> Configuring GitHub OIDC federation' -ForegroundColor Cyan
az identity federated-credential delete --name github-dev --identity-name $identityName --resource-group $stateRg --yes 2>$null
$global:LASTEXITCODE = 0
Invoke-Az {
    az identity federated-credential create `
        --name github-dev `
        --identity-name $identityName `
        --resource-group $stateRg `
        --issuer 'https://token.actions.githubusercontent.com' `
        --subject $oidcSubject `
        --audiences 'api://AzureADTokenExchange' `
        --output none
}

$createdSubject = az identity federated-credential show --name github-dev --identity-name $identityName --resource-group $stateRg --query subject -o tsv
if ($createdSubject -ne $oidcSubject) { throw "Federated credential subject mismatch. Expected '$oidcSubject' but Azure returned '$createdSubject'." }

Write-Host '==> Assigning Azure roles to the GitHub deployment identity' -ForegroundColor Cyan
$subscriptionScope = "/subscriptions/$subscriptionId"
$stateScope = az storage account show --name $stateAccount --resource-group $stateRg --query id -o tsv

# Contributor creates resources. RBAC Administrator is needed because Terraform creates
# Managed Identity role assignments for App Service and Functions. Scope is intentionally
# broad for the disposable Dev bootstrap; tighten this before Production.
Ensure-RoleAssignment -PrincipalId $principalId -Role 'Contributor' -Scope $subscriptionScope
Ensure-RoleAssignment -PrincipalId $principalId -Role 'Role Based Access Control Administrator' -Scope $subscriptionScope
Ensure-RoleAssignment -PrincipalId $principalId -Role 'Storage Blob Data Contributor' -Scope $stateScope

Write-Host '==> Creating GitHub dev environment and configuration' -ForegroundColor Cyan
Invoke-Gh { gh api --method PUT "repos/$GitHubOwner/$GitHubRepository/environments/dev" --silent }

Invoke-Gh { gh variable set AZURE_CLIENT_ID --env dev --repo "$GitHubOwner/$GitHubRepository" --body $clientId }
Invoke-Gh { gh variable set AZURE_TENANT_ID --env dev --repo "$GitHubOwner/$GitHubRepository" --body $tenantId }
Invoke-Gh { gh variable set AZURE_SUBSCRIPTION_ID --env dev --repo "$GitHubOwner/$GitHubRepository" --body $subscriptionId }
Invoke-Gh { gh variable set AZURE_FEDERATED_SUBJECT --env dev --repo "$GitHubOwner/$GitHubRepository" --body $oidcSubject }
Invoke-Gh { gh variable set TFSTATE_RESOURCE_GROUP --env dev --repo "$GitHubOwner/$GitHubRepository" --body $stateRg }
Invoke-Gh { gh variable set TFSTATE_STORAGE_ACCOUNT --env dev --repo "$GitHubOwner/$GitHubRepository" --body $stateAccount }
Invoke-Gh { gh variable set TFSTATE_CONTAINER --env dev --repo "$GitHubOwner/$GitHubRepository" --body $container }
Invoke-Gh { gh variable set TFSTATE_KEY --env dev --repo "$GitHubOwner/$GitHubRepository" --body $key }

$SqlAdminPassword | gh secret set SQL_ADMIN_PASSWORD --env dev --repo "$GitHubOwner/$GitHubRepository"
if ($LASTEXITCODE -ne 0) { throw 'Could not set SQL_ADMIN_PASSWORD GitHub environment secret.' }
$JwtSigningKey | gh secret set JWT_SIGNING_KEY --env dev --repo "$GitHubOwner/$GitHubRepository"
if ($LASTEXITCODE -ne 0) { throw 'Could not set JWT_SIGNING_KEY GitHub environment secret.' }
$AlertEmail | gh secret set ALERT_EMAIL --env dev --repo "$GitHubOwner/$GitHubRepository"
if ($LASTEXITCODE -ne 0) { throw 'Could not set ALERT_EMAIL GitHub environment secret.' }

$backendPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'infra/environments/dev/backend.hcl'
@"
resource_group_name  = "$stateRg"
storage_account_name = "$stateAccount"
container_name       = "$container"
key                  = "$key"
use_azuread_auth     = true
"@ | Set-Content -Encoding utf8 $backendPath

Write-Host '`nBOOTSTRAP COMPLETE' -ForegroundColor Green
Write-Host "Repository:            $GitHubOwner/$GitHubRepository"
Write-Host "GitHub owner ID:       $ownerId"
Write-Host "GitHub repository ID:  $repoId"
Write-Host "OIDC subject:          $oidcSubject"
Write-Host "Azure client ID:       $clientId"
Write-Host "Azure tenant ID:       $tenantId"
Write-Host "Azure subscription ID: $subscriptionId"
Write-Host "Terraform state RG:    $stateRg"
Write-Host "Terraform state acct:  $stateAccount"
Write-Host '`nGitHub environment variables/secrets were configured automatically.'
Write-Host 'Wait about 60 seconds for Azure role/federation propagation before the first Terraform workflow.' -ForegroundColor Yellow
