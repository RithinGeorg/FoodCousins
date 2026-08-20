# Start Here

1. Run `./scripts/build-local.ps1` and make sure it is green.
2. Create a brand-new empty GitHub repository and push this repository to `main`.
3. Run `az login` and `gh auth login`.
4. Run `./scripts/bootstrap-azure-github-dev.ps1 -GitHubOwner "..." -GitHubRepository "..." -AlertEmail "..."`.
5. Run `./scripts/verify-dev-oidc.ps1 ...` and make sure it says `OIDC configuration matches.`
6. Run `./scripts/create-deployment-branches.ps1`.
7. GitHub Actions → Terraform Dev → select branch `terraform-dev` → `plan`.
8. Review the plan, then Terraform Dev → branch `terraform-dev` → `apply`.
9. GitHub Actions → Deploy Dev → branch `dev-deploy`.
10. Test: register cook → add food → register customer → discover cousins → submit Cook at Home request → verify pricing Function → confirm quote → cook accepts/progresses order.

Do not run Terraform Dev from `main`. The workflow will intentionally reject the wrong source branch.
