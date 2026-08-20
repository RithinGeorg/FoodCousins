Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$branches = @('develop', 'terraform-dev', 'dev-deploy')

git switch main
if ($LASTEXITCODE -ne 0) { throw 'Could not switch to main.' }
git pull origin main
if ($LASTEXITCODE -ne 0) { throw 'Could not update main.' }

foreach ($branch in $branches) {
    git show-ref --verify --quiet "refs/remotes/origin/$branch"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "$branch already exists on origin; skipping."
        continue
    }
    $global:LASTEXITCODE = 0
    git switch -c $branch main
    if ($LASTEXITCODE -ne 0) { throw "Could not create $branch." }
    git push -u origin $branch
    if ($LASTEXITCODE -ne 0) { throw "Could not push $branch." }
    git switch main
}

Write-Host 'Branches ready: main, develop, terraform-dev, dev-deploy' -ForegroundColor Green
