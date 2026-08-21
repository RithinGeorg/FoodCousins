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

    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI command failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Gh {
    param([Parameter(Mandatory=$true)][scriptblock]$Command)

    & $Command

    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI command failed with exit code $LASTEXITCODE."
    }
}

function Ensure-RoleAssignment {
    param(
        [Parameter(Mandatory=$true)][string]$PrincipalId,
        [Parameter(Mandatory=$true)][string]$Role,
        [Parameter(Mandatory=$true)][string]$Scope
    )

    $existing = az role assignment list `
        --assignee-object-id $PrincipalId `
        --role $Role `
        --scope $Scope `
        --query "[0].id" `
        -o tsv

    if ($LASTEXITCODE -ne 0) {
        throw "Could not check Azure role assignment '$Role'."
    }

    if ([string]::IsNullOrWhiteSpace($existing)) {
        Write-Host "    Assigning role: $Role" -ForegroundColor DarkCyan

        Invoke-Az {
            az role assignment create `
                --assignee-object-id $PrincipalId `
                --assignee-principal-type ServicePrincipal `
                --role $Role `
                --scope $Scope `
                --output none
        }
    }
    else {
        Write-Host "    Role already assigned: $Role" -ForegroundColor DarkGray
    }
}

function New-SafeRandomString([int]$Bytes = 32) {
    # Compatible with Windows PowerShell 5.1.
    $buffer = New-Object byte[] $Bytes
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()

    try {
        $rng.GetBytes($buffer)
    }
    finally {
        $rng.Dispose()
    }

    return [Convert]::ToBase64String($buffer).
        Replace('+', 'A').
        Replace('/', 'B').
        Replace('=', '')
}

function New-StrongSqlPassword {
    # Ensure Azure SQL password-complexity categories are represented.
    return 'Fc!9aA' + (New-SafeRandomString 24)
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) is required.'
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) is required.'
}

Write-Host '==> Verifying Azure login' -ForegroundColor Cyan

$accountJson = az account show -o json

if ($LASTEXITCODE -ne 0) {
    throw "Run 'az login' first."
}

$account = $accountJson | ConvertFrom-Json
$subscriptionId = [string]$account.id
$tenantId = [string]$account.tenantId

Write-Host "    Subscription: $subscriptionId"
Write-Host "    Tenant:       $tenantId"

Write-Host '==> Verifying GitHub login and repository' -ForegroundColor Cyan

gh auth status | Out-Host

if ($LASTEXITCODE -ne 0) {
    throw "Run 'gh auth login' first."
}

$repoJson = gh api "repos/$GitHubOwner/$GitHubRepository"

if ($LASTEXITCODE -ne 0) {
    throw "GitHub repository $GitHubOwner/$GitHubRepository was not found or is not accessible."
}

$repo = $repoJson | ConvertFrom-Json
$repoId = [string]$repo.id
$ownerId = [string]$repo.owner.id

if ([string]::IsNullOrWhiteSpace($repoId) -or [string]::IsNullOrWhiteSpace($ownerId)) {
    throw 'Could not read the immutable GitHub repository/owner IDs.'
}

# New GitHub repositories use immutable owner/repository IDs in the default OIDC subject.
$oidcSubject = "repo:${GitHubOwner}@${ownerId}/${GitHubRepository}@${repoId}:environment:dev"

Write-Host "    Owner ID:      $ownerId"
Write-Host "    Repository ID: $repoId"
Write-Host "    OIDC subject:  $oidcSubject"

if ([string]::IsNullOrWhiteSpace($SqlAdminPassword)) {
    $SqlAdminPassword = New-StrongSqlPassword
}

if ([string]::IsNullOrWhiteSpace($JwtSigningKey)) {
    $JwtSigningKey = New-SafeRandomString 48
}

if ($SqlAdminPassword.Length -lt 16) {
    throw 'SQL admin password must be at least 16 characters.'
}

if ($JwtSigningKey.Length -lt 32) {
    throw 'JWT signing key must be at least 32 characters.'
}

$stateRg = 'rg-foodcousins-tfstate'
$container = 'tfstate'
$key = 'dev.terraform.tfstate'
$identityName = 'id-foodcousins-github-dev'

Write-Host '==> Creating/reusing Terraform state resources' -ForegroundColor Cyan

Invoke-Az {
    az group create `
        --name $stateRg `
        --location $Location `
        --output none
}

# Reuse a state account from a previous partial bootstrap if one is already present.
$stateAccount = az storage account list `
    --resource-group $stateRg `
    --query "[?starts_with(name, 'stfctf')].name | [0]" `
    -o tsv

if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect Terraform state storage accounts.'
}

if ([string]::IsNullOrWhiteSpace($stateAccount)) {
    $created = $false

    for ($attempt = 1; $attempt -le 5 -and -not $created; $attempt++) {
        $suffix = Get-Random -Minimum 100000 -Maximum 999999
        $candidate = "stfctf$suffix"

        Write-Host "    Creating state storage account: $candidate"

        az storage account create `
            --name $candidate `
            --resource-group $stateRg `
            --location $Location `
            --sku Standard_LRS `
            --kind StorageV2 `
            --min-tls-version TLS1_2 `
            --allow-blob-public-access false `
            --output none

        if ($LASTEXITCODE -eq 0) {
            $stateAccount = $candidate
            $created = $true
        }
        else {
            Write-Host "    Storage-account name unavailable; retrying ($attempt/5)." -ForegroundColor Yellow
            $global:LASTEXITCODE = 0
        }
    }

    if (-not $created) {
        throw 'Could not create the Terraform state storage account after 5 attempts.'
    }
}
else {
    Write-Host "    Reusing state storage account: $stateAccount" -ForegroundColor DarkGray
}

Invoke-Az {
    az storage container create `
        --name $container `
        --account-name $stateAccount `
        --auth-mode key `
        --output none
}

Write-Host '==> Creating/reusing GitHub deployment managed identity' -ForegroundColor Cyan

# IMPORTANT: use LIST here. `az identity show` on a missing identity writes a ResourceNotFound
# error to stderr, which Windows PowerShell 5.1 can promote to a terminating error.
$identityResourceId = az identity list `
    --resource-group $stateRg `
    --query "[?name=='$identityName'].id | [0]" `
    -o tsv

if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect managed identities.'
}

if ([string]::IsNullOrWhiteSpace($identityResourceId)) {
    Write-Host "    Creating managed identity: $identityName"

    Invoke-Az {
        az identity create `
            --name $identityName `
            --resource-group $stateRg `
            --location $Location `
            --output none
    }
}
else {
    Write-Host "    Managed identity already exists: $identityName" -ForegroundColor DarkGray
}

$clientId = az identity show `
    --name $identityName `
    --resource-group $stateRg `
    --query clientId `
    -o tsv

if ($LASTEXITCODE -ne 0) {
    throw 'Could not read managed identity client ID.'
}

$principalId = az identity show `
    --name $identityName `
    --resource-group $stateRg `
    --query principalId `
    -o tsv

if ($LASTEXITCODE -ne 0) {
    throw 'Could not read managed identity principal ID.'
}

if ([string]::IsNullOrWhiteSpace($clientId) -or [string]::IsNullOrWhiteSpace($principalId)) {
    throw 'Could not read managed identity IDs.'
}

Write-Host "    Client ID:    $clientId"
Write-Host "    Principal ID: $principalId"

Write-Host '==> Configuring GitHub OIDC federation' -ForegroundColor Cyan

$existingFicSubject = az identity federated-credential list `
    --identity-name $identityName `
    --resource-group $stateRg `
    --query "[?name=='github-dev'].subject | [0]" `
    -o tsv

if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect managed-identity federated credentials.'
}

if (-not [string]::IsNullOrWhiteSpace($existingFicSubject) -and $existingFicSubject -ne $oidcSubject) {
    Write-Host '    Existing github-dev credential has a different subject; replacing it.' -ForegroundColor Yellow

    Invoke-Az {
        az identity federated-credential delete `
            --name github-dev `
            --identity-name $identityName `
            --resource-group $stateRg `
            --yes
    }

    $existingFicSubject = ''
}

if ([string]::IsNullOrWhiteSpace($existingFicSubject)) {
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
}
else {
    Write-Host '    Federated credential already has the expected subject.' -ForegroundColor DarkGray
}

$createdSubject = az identity federated-credential show `
    --name github-dev `
    --identity-name $identityName `
    --resource-group $stateRg `
    --query subject `
    -o tsv

if ($LASTEXITCODE -ne 0) {
    throw 'Could not verify the federated credential.'
}

if ($createdSubject -ne $oidcSubject) {
    throw "Federated credential subject mismatch. Expected '$oidcSubject' but Azure returned '$createdSubject'."
}

Write-Host '==> Assigning Azure roles to the GitHub deployment identity' -ForegroundColor Cyan

$subscriptionScope = "/subscriptions/$subscriptionId"

$stateScope = az storage account show `
    --name $stateAccount `
    --resource-group $stateRg `
    --query id `
    -o tsv

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($stateScope)) {
    throw 'Could not read Terraform state storage-account scope.'
}

# Dev bootstrap: Terraform creates resources and RBAC assignments.
Ensure-RoleAssignment `
    -PrincipalId $principalId `
    -Role 'Contributor' `
    -Scope $subscriptionScope

Ensure-RoleAssignment `
    -PrincipalId $principalId `
    -Role 'Role Based Access Control Administrator' `
    -Scope $subscriptionScope

Ensure-RoleAssignment `
    -PrincipalId $principalId `
    -Role 'Storage Blob Data Contributor' `
    -Scope $stateScope

Write-Host '==> Creating GitHub dev environment and configuration' -ForegroundColor Cyan

Invoke-Gh {
    gh api `
        --method PUT `
        "repos/$GitHubOwner/$GitHubRepository/environments/dev" `
        --silent
}

Invoke-Gh {
    gh variable set AZURE_CLIENT_ID `
        --env dev `
        --repo "$GitHubOwner/$GitHubRepository" `
        --body $clientId
}

Invoke-Gh {
    gh variable set AZURE_TENANT_ID `
        --env dev `
        --repo "$GitHubOwner/$GitHubRepository" `
        --body $tenantId
}

Invoke-Gh {
    gh variable set AZURE_SUBSCRIPTION_ID `
        --env dev `
        --repo "$GitHubOwner/$GitHubRepository" `
        --body $subscriptionId
}

Invoke-Gh {
    gh variable set AZURE_FEDERATED_SUBJECT `
        --env dev `
        --repo "$GitHubOwner/$GitHubRepository" `
        --body $oidcSubject
}

Invoke-Gh {
    gh variable set TFSTATE_RESOURCE_GROUP `
        --env dev `
        --repo "$GitHubOwner/$GitHubRepository" `
        --body $stateRg
}

Invoke-Gh {
    gh variable set TFSTATE_STORAGE_ACCOUNT `
        --env dev `
        --repo "$GitHubOwner/$GitHubRepository" `
        --body $stateAccount
}

Invoke-Gh {
    gh variable set TFSTATE_CONTAINER `
        --env dev `
        --repo "$GitHubOwner/$GitHubRepository" `
        --body $container
}

Invoke-Gh {
    gh variable set TFSTATE_KEY `
        --env dev `
        --repo "$GitHubOwner/$GitHubRepository" `
        --body $key
}

$SqlAdminPassword | gh secret set SQL_ADMIN_PASSWORD `
    --env dev `
    --repo "$GitHubOwner/$GitHubRepository"

if ($LASTEXITCODE -ne 0) {
    throw 'Could not set SQL_ADMIN_PASSWORD GitHub environment secret.'
}

$JwtSigningKey | gh secret set JWT_SIGNING_KEY `
    --env dev `
    --repo "$GitHubOwner/$GitHubRepository"

if ($LASTEXITCODE -ne 0) {
    throw 'Could not set JWT_SIGNING_KEY GitHub environment secret.'
}

$AlertEmail | gh secret set ALERT_EMAIL `
    --env dev `
    --repo "$GitHubOwner/$GitHubRepository"

if ($LASTEXITCODE -ne 0) {
    throw 'Could not set ALERT_EMAIL GitHub environment secret.'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendPath = Join-Path $repoRoot 'infra/environments/dev/backend.hcl'

@"
resource_group_name  = "$stateRg"
storage_account_name = "$stateAccount"
container_name       = "$container"
key                  = "$key"
use_azuread_auth     = true
"@ | Set-Content -Encoding utf8 $backendPath

Write-Host ''
Write-Host 'BOOTSTRAP COMPLETE' -ForegroundColor Green
Write-Host "Repository:            $GitHubOwner/$GitHubRepository"
Write-Host "GitHub owner ID:       $ownerId"
Write-Host "GitHub repository ID:  $repoId"
Write-Host "OIDC subject:          $oidcSubject"
Write-Host "Azure client ID:       $clientId"
Write-Host "Azure tenant ID:       $tenantId"
Write-Host "Azure subscription ID: $subscriptionId"
Write-Host "Terraform state RG:    $stateRg"
Write-Host "Terraform state acct:  $stateAccount"
Write-Host ''
Write-Host 'GitHub dev environment variables/secrets were configured automatically.'
Write-Host 'Wait about 60 seconds for Azure role/federation propagation before the first Terraform workflow.' -ForegroundColor Yellow
