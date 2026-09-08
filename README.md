# FoodCousins — .NET 10 Dev Baseline

FoodCousins is a .NET 10 + React/TypeScript Azure application for food discovery, home-cooked food orders, and **Cook at Home** requests.

This repository is intentionally organized so that **Terraform infrastructure deployment and Dev application deployment are separate workflows and separate long-lived branches**.

## Technology baseline

- ASP.NET Core .NET 10 / C# 14
- Clean Architecture projects: API, Application, Domain, Infrastructure
- React + TypeScript + Vite
- Azure Static Web Apps for React
- Azure App Service (Linux B1) for API, Always On enabled
- Azure SQL
- Azure Blob Storage for food images
- Azure Key Vault
- Azure Service Bus
- Azure Functions .NET 10 isolated on Flex Consumption
- **Azure Functions instance memory = 2048 MB minimum**
- Application Insights + Log Analytics
- Serilog structured logging
- GitHub Actions + Azure OIDC
- Terraform

## Key implementation rules

- Browser JWT access and refresh tokens are `Secure` + `HttpOnly` cookies in Azure.
- Refresh tokens are hashed before they are stored in SQL.
- React never reads or stores JWTs.
- Important write operations use `Idempotency-Key`.
- Orders and Outbox messages are committed in the same SQL transaction.
- The API `BackgroundService` dispatches Outbox messages to Service Bus.
- Azure Functions catch exceptions, log structured context, and rethrow failures so Service Bus retries/DLQ behavior remains active.
- API exceptions are handled centrally by `ApiExceptionMiddleware`.
- Do not swallow exceptions and do not double-log the same exception at every layer.
- Sensitive values such as passwords, JWTs, cookies, connection strings and payment data must not be logged.

## Cook at Home flow

1. Customer chooses a food/cook.
2. Customer submits people count, location, requested time, contact details and instructions.
3. API writes the `CookAtHomeOrder`, status history and `CookAtHomeOrderRequested` Outbox event transactionally.
4. API Outbox dispatcher publishes `CookAtHomeOrderRequested` to `order-processing`.
5. `OrderPricingFunction` receives the message, moves the order to `Processing`, calculates the Dev quote and moves it to `PriceCalculated`.
6. The Function writes a `CookAtHomeQuoteReady` Outbox event.
7. API Outbox dispatcher publishes notification events to the `notifications` queue.
8. `OrderEventsFunction` consumes notification events and records telemetry. Add a real email/SMS provider here later.
9. Customer confirms the quote, then the cook can accept/reject and progress the job.

The current Dev pricing rule is deliberately simple: `Food.Price × PeopleCount`. Replace this with ingredient/travel/service pricing when the customer's pricing rules are final.

## Branch model

Keep these long-lived branches:

```text
main            stable source / workflow definitions
develop         normal development integration
terraform-dev   source branch for Terraform Dev plan/apply
dev-deploy      source branch for application Dev deployment
```

Temporary feature/fix branches should be deleted after merge.

## Workflows

```text
.github/workflows/ci.yml
.github/workflows/terraform-dev.yml
.github/workflows/deploy-dev.yml
```

- `CI`: build/test/React/Terraform validation. Never changes Azure.
- `Terraform Dev`: manual only and refuses to run unless `terraform-dev` is selected in the Run workflow branch dropdown.
- `Deploy Dev`: manual only and refuses to run unless `dev-deploy` is selected.

## Start completely from zero

### 1. Prerequisites

Install and authenticate:

- .NET SDK `10.0.400`
- Node.js 24 + npm
- Terraform `1.12.2`
- Azure CLI
- GitHub CLI

Then:

```powershell
az login
gh auth login
```

### 2. Build before Azure

From the repository root:

```powershell
.\scripts\build-local.ps1
```

Do not continue until the final line is:

```text
SUCCESS: FoodCousins local build checks passed.
```

The script fails immediately if `dotnet`, `npm` or `terraform` returns a non-zero exit code.

When you edit Terraform, format it before committing:

```powershell
terraform fmt -recursive .\infra
```

### 3. Create a new empty GitHub repository

Create the repository first because GitHub's immutable OIDC subject contains the permanent repository ID.

Then push this code to `main`:

```powershell
git init
git add .
git commit -m "Initial FoodCousins .NET 10 baseline"
git branch -M main
git remote add origin https://github.com/YOUR_OWNER/YOUR_REPOSITORY.git
git push -u origin main
```

### 4. Bootstrap Azure state + GitHub OIDC

Run:

```powershell
.\scripts\bootstrap-azure-github-dev.ps1 `
  -GitHubOwner "YOUR_OWNER" `
  -GitHubRepository "YOUR_REPOSITORY" `
  -AlertEmail "YOUR_EMAIL"
```

The script:

- reads the current Azure subscription/tenant from `az login`
- reads immutable GitHub owner/repository IDs through `gh api`
- creates only the Terraform state resource group/storage
- creates a **user-assigned managed identity** for GitHub Actions
- creates its federated credential using the new immutable GitHub subject format
- assigns Dev bootstrap Azure roles
- creates GitHub Environment `dev`
- writes Azure IDs/state information as GitHub **environment variables**
- writes SQL/JWT/email values as GitHub **environment secrets**
- saves a local, gitignored `infra/environments/dev/backend.hcl`

For repositories created after July 15, 2026, GitHub's default OIDC subject has this shape:

```text
repo:OWNER@OWNER_ID/REPOSITORY@REPOSITORY_ID:environment:dev
```

The bootstrap script builds this dynamically. Do not hard-code old repository IDs.

You can verify the Azure and GitHub subject match with:

```powershell
.\scripts\verify-dev-oidc.ps1 `
  -GitHubOwner "YOUR_OWNER" `
  -GitHubRepository "YOUR_REPOSITORY"
```

### 5. Create the long-lived branches

```powershell
.\scripts\create-deployment-branches.ps1
```

This creates/pushes:

```text
develop
terraform-dev
dev-deploy
```

### 6. Terraform Dev

GitHub → Actions → **Terraform Dev** → Run workflow

Choose:

```text
Branch: terraform-dev
Action: plan
```

Review the plan. Only when correct, run again:

```text
Branch: terraform-dev
Action: apply
```

Terraform creates:

- `rg-foodcousins-dev`
- Log Analytics + Application Insights
- Static Web App
- App Service B1 + .NET 10 API
- Azure SQL server/database
- Blob storage
- Key Vault
- Service Bus namespace
- `order-processing` queue
- `notifications` queue
- Flex Consumption Function App
- **2048 MB Function memory**
- Managed identities + RBAC
- budget/5xx alerting

After apply, the workflow stores the SQL runtime connection string and JWT signing key in Key Vault.

### 7. Deploy the application

Promote the code you want to deploy into `dev-deploy`, then:

GitHub → Actions → **Deploy Dev** → Run workflow

Choose:

```text
Branch: dev-deploy
```

This workflow does not run Terraform. It only builds/tests and deploys:

- API → existing App Service
- Functions → existing Function App
- React → existing Static Web App
- `/health` smoke test

## GitHub `dev` environment layout

The bootstrap script configures these automatically.

Environment variables (`vars`):

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_SUBSCRIPTION_ID
AZURE_FEDERATED_SUBJECT
TFSTATE_RESOURCE_GROUP
TFSTATE_STORAGE_ACCOUNT
TFSTATE_CONTAINER
TFSTATE_KEY
```

Environment secrets (`secrets`):

```text
SQL_ADMIN_PASSWORD
JWT_SIGNING_KEY
ALERT_EMAIL
```

The workflows intentionally use `vars.AZURE_*`, not `secrets.AZURE_*`.

## Serilog + Application Insights

The application uses Serilog for structured application logging and sends traces to Application Insights. The repository pins Application Insights SDK `2.23.0` because `Serilog.Sinks.ApplicationInsights 5.0.1` requires Application Insights `< 3.0.0`; this avoids the NU1608 warning that occurred with Application Insights 3.x.

API request logging includes correlation context but does not log request bodies, cookies, JWTs or authorization headers.

## Exception handling

### API

- `ApiExceptionMiddleware` is the request boundary.
- Expected validation/not-found/auth/conflict failures are translated to safe HTTP errors.
- Unexpected failures are logged with Serilog and return generic Problem Details.
- Lower-level code catches only when it can recover, add context, translate, clean up, or implement retry behavior.

### Functions

Both Functions have explicit `try/catch` boundaries:

- cancellation: log + rethrow
- `ServiceBusException`: log SDK context + rethrow
- unexpected exception: log invocation/message context + rethrow

Rethrowing is intentional so Service Bus retries and eventually dead-letters poison messages.

## Dev database initialization

Dev currently uses `Database:EnsureCreated=true` so a brand-new disposable Dev database can start immediately. Before Production, replace this with reviewed EF Core migrations applied during deployment.

## Current exclusions

Not included yet:

- payment provider integration
- real email/SMS provider
- Redis
- API Management
- Front Door/Application Gateway
- AKS/microservices
- Production infrastructure

