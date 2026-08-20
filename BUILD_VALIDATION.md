# Build validation before Azure deployment

Azure deployment is not the first test of the repository.

## Local Windows validation

Prerequisites:
- .NET SDK 10.0.400
- Node.js 24 + npm
- Terraform 1.12.2 (recommended)

From the repository root:

```powershell
.\scripts\build-local.ps1
```

The script runs:
1. `dotnet restore`
2. `dotnet build -c Release`
3. `dotnet test -c Release`
4. frontend dependency install (`npm ci` when a lock file exists, otherwise `npm install`)
5. `npm run build`
6. `terraform init -backend=false` and `terraform validate` when Terraform is installed

It checks native command exit codes and stops on failure. Do not trust a green-looking line above an error; the final line must be:

```text
SUCCESS: FoodCousins local build checks passed.
```

If you change Terraform, format it locally before committing:

```powershell
terraform fmt -recursive .\infra
```

Formatting is deliberately not a deployment gate; `terraform validate` is.

## GitHub CI

`.github/workflows/ci.yml` builds/tests .NET, builds React, and validates Terraform. It never changes Azure.

## Separate Dev workflows

- `Terraform Dev` is manual-only and must be run from `terraform-dev`.
- `Deploy Dev` is manual-only and must be run from `dev-deploy`.
- `Deploy Dev` contains no Terraform commands.

The OIDC workflows verify the actual GitHub OIDC `sub` claim against the immutable subject calculated during bootstrap before attempting Azure login.
