param(
    [Parameter(Mandatory=$true)][string]$GitHubOwner,
    [Parameter(Mandatory=$true)][string]$GitHubRepository
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = (gh api "repos/$GitHubOwner/$GitHubRepository" | ConvertFrom-Json)
$expected = "repo:${GitHubOwner}@$($repo.owner.id)/${GitHubRepository}@$($repo.id):environment:dev"
$clientId = gh variable get AZURE_CLIENT_ID --env dev --repo "$GitHubOwner/$GitHubRepository"
$configuredSubject = gh variable get AZURE_FEDERATED_SUBJECT --env dev --repo "$GitHubOwner/$GitHubRepository"

Write-Host "Expected GitHub OIDC subject : $expected"
Write-Host "GitHub configured subject    : $configuredSubject"
Write-Host "GitHub Azure client ID       : $clientId"

if ($expected -ne $configuredSubject) {
    throw 'GitHub AZURE_FEDERATED_SUBJECT does not match the current repository immutable IDs.'
}

$identity = az identity list --query "[?clientId=='$clientId'] | [0]" -o json | ConvertFrom-Json
if ($null -eq $identity -or [string]::IsNullOrWhiteSpace([string]$identity.name)) {
    throw 'Could not find an Azure user-assigned managed identity matching AZURE_CLIENT_ID.'
}

$azureSubject = az identity federated-credential show `
    --name github-dev `
    --identity-name $identity.name `
    --resource-group $identity.resourceGroup `
    --query subject `
    -o tsv

Write-Host "Azure federated subject     : $azureSubject"

if ($expected -ne $azureSubject) {
    throw 'Azure federated credential subject does not match GitHub expected subject.'
}

Write-Host 'OIDC configuration matches.' -ForegroundColor Green
