# GillyTracker

Single-page app for reporting Gilly's location.

## What it does

- Attempts to collect reporter geolocation when the page loads
- Falls back to manual latitude/longitude entry if location access is unavailable
- Lets the reporter submit contact details/notes
- Stores reports in SQL table `GillyTracker.DogSightingReports`

## Local development

```bash
dotnet test --project /home/runner/work/GillyTracker/GillyTracker/GillyTracker.Core.Tests/GillyTracker.Core.Tests.csproj
cd /home/runner/work/GillyTracker/GillyTracker/GillyTracker.Web
pnpm install --frozen-lockfile
pnpm lint
pnpm build
```

## Infrastructure

Terraform is configured to use existing Azure resources:

- Resource Group: `KebooDev`
- ACR: `keboodevacr.azurecr.io`
- Container App Environment: `keboodev-env`
- SQL Server and database are discovered via Terraform data sources in the existing resource group
- Frontend hosting is provisioned as an Azure Static Web App, and backend CORS allows that origin
- The deploy-infrastructure workflow expects repository secrets `GITHUB_ACTIONS_APP_OBJECT_ID` and `GITHUB_ACTIONS_INFRA_OBJECT_ID` for database user principal IDs
