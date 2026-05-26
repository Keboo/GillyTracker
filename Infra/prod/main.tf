locals {
  environment = var.environment
  tags = merge(var.tags,
    {
      "Environment" = local.environment
  })
  sql_server_name   = "keboodev-sql"
  sql_database_name = "keboodevdb"

  base_database_connection_string = "Server=tcp:${data.azurerm_mssql_server.existing.fully_qualified_domain_name},1433;Initial Catalog=${data.azurerm_mssql_database.existing.name};Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;"
  database_connection_string      = "${local.base_database_connection_string}Authentication=\"Active Directory Default\";"
  connection_string_no_auth       = local.base_database_connection_string
  key_vault_name                  = "gilly${lower(local.environment)}${random_string.key_vault_suffix.result}kv"
  db_permissions = [
    "db_datareader",
    "db_datawriter",
    "db_ddladmin"
  ]
}

data "azurerm_resource_group" "resource_group" {
  name = var.existing_resource_group_name
}

data "azurerm_client_config" "current" {}

resource "random_string" "key_vault_suffix" {
  length  = 5
  special = false
  upper   = false
}

resource "azurerm_key_vault" "app" {
  name                          = local.key_vault_name
  location                      = data.azurerm_resource_group.resource_group.location
  resource_group_name           = data.azurerm_resource_group.resource_group.name
  tenant_id                     = data.azurerm_client_config.current.tenant_id
  sku_name                      = "standard"
  rbac_authorization_enabled    = true
  soft_delete_retention_days    = 90
  purge_protection_enabled      = true
  public_network_access_enabled = true

  tags = local.tags
}

resource "azurerm_user_assigned_identity" "app_identity" {
  name                = "gillytracker-${lower(local.environment)}-mi"
  location            = data.azurerm_resource_group.resource_group.location
  resource_group_name = data.azurerm_resource_group.resource_group.name

  tags = local.tags
}

resource "azurerm_role_assignment" "app_identity_key_vault_secrets_user" {
  scope                = azurerm_key_vault.app.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.app_identity.principal_id
}

resource "azurerm_role_assignment" "current_principal_key_vault_secrets_officer" {
  count = data.azurerm_client_config.current.object_id == data.azuread_service_principal.provisioning_principal.object_id ? 0 : 1

  scope                = azurerm_key_vault.app.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_role_assignment" "provisioning_principal_key_vault_secrets_officer" {
  scope                = azurerm_key_vault.app.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azuread_service_principal.provisioning_principal.object_id
}

data "azurerm_container_app_environment" "existing" {
  name                = "keboodev-env"
  resource_group_name = data.azurerm_resource_group.resource_group.name
}

data "azurerm_container_registry" "existing" {
  name                = "keboodevacr"
  resource_group_name = data.azurerm_resource_group.resource_group.name
}

resource "azurerm_role_assignment" "app_identity_acr_pull" {
  scope                = data.azurerm_container_registry.existing.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.app_identity.principal_id
}

data "azurerm_mssql_server" "existing" {
  name                = local.sql_server_name
  resource_group_name = data.azurerm_resource_group.resource_group.name
}

data "azurerm_mssql_database" "existing" {
  name      = local.sql_database_name
  server_id = data.azurerm_mssql_server.existing.id
}

data "azuread_service_principal" "database_users" {
  for_each = var.database_users_client_ids

  client_id = each.value
}

data "azuread_service_principal" "provisioning_principal" {
  client_id = var.provisioning_client_id
}

data "azuread_group" "pet_tracker_admins" {
  display_name = "PetTrackerAdmins"
}

resource "azuread_application" "backend_auth" {
  display_name            = "gillytracker-${lower(local.environment)}-backend-auth"
  sign_in_audience        = "AzureADMyOrg"
  group_membership_claims = ["SecurityGroup"]

  web {
    redirect_uris = ["https://api.dogtracker.keboo.dev/signin-microsoft"]
  }
}

resource "azuread_service_principal" "backend_auth" {
  client_id = azuread_application.backend_auth.client_id
}

resource "azuread_application_password" "backend_auth" {
  application_id = azuread_application.backend_auth.id
  display_name   = "terraform-generated-client-secret"
}

resource "azurerm_key_vault_secret" "microsoft_tenant_id" {
  name         = "Authentication--Microsoft--TenantId"
  value        = data.azurerm_client_config.current.tenant_id
  key_vault_id = azurerm_key_vault.app.id

  depends_on = [
    azurerm_role_assignment.current_principal_key_vault_secrets_officer,
    azurerm_role_assignment.provisioning_principal_key_vault_secrets_officer
  ]
}

resource "azurerm_key_vault_secret" "microsoft_client_id" {
  name         = "Authentication--Microsoft--ClientId"
  value        = azuread_application.backend_auth.client_id
  key_vault_id = azurerm_key_vault.app.id

  depends_on = [
    azurerm_role_assignment.current_principal_key_vault_secrets_officer,
    azurerm_role_assignment.provisioning_principal_key_vault_secrets_officer
  ]
}

resource "azurerm_key_vault_secret" "microsoft_client_secret" {
  name         = "Authentication--Microsoft--ClientSecret"
  value        = azuread_application_password.backend_auth.value
  key_vault_id = azurerm_key_vault.app.id

  depends_on = [
    azurerm_role_assignment.current_principal_key_vault_secrets_officer,
    azurerm_role_assignment.provisioning_principal_key_vault_secrets_officer
  ]
}

resource "azurerm_key_vault_secret" "pet_tracker_admins_group_object_id" {
  name         = "Authorization--PetTrackerAdminsGroupObjectId"
  value        = data.azuread_group.pet_tracker_admins.object_id
  key_vault_id = azurerm_key_vault.app.id

  depends_on = [
    azurerm_role_assignment.current_principal_key_vault_secrets_officer,
    azurerm_role_assignment.provisioning_principal_key_vault_secrets_officer
  ]
}

resource "terraform_data" "setup_database_user" {
  triggers_replace = [
    data.azurerm_mssql_database.existing.id,
    jsonencode(var.database_users_client_ids),
    var.provisioning_client_id,
    join(",", local.db_permissions),
    join(",", [for username in sort(keys(data.azuread_service_principal.database_users)) : "${username}:${data.azuread_service_principal.database_users[username].object_id}"]),
    "v10"
  ]

  provisioner "local-exec" {
    command = <<-EOT
      $ErrorActionPreference = 'Stop'
      $ipRuleName = $null

      try {
        $currentIp = (Invoke-RestMethod -Uri "https://api.ipify.org").ToString()
        $ruleSuffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
        $ipRuleName = "TerraformTemp-DbUsers-$ruleSuffix"

        Write-Host "Installing SqlServer module..."
        Install-Module -Name SqlServer -AcceptLicense -Force -ErrorAction SilentlyContinue
        Import-Module SqlServer -ErrorAction Stop
        Write-Host "SqlServer module loaded successfully"

        Write-Host "Creating temporary firewall rule for IP: $currentIp"
        $firewallOutput = az sql server firewall-rule create `
          --resource-group '${data.azurerm_resource_group.resource_group.name}' `
          --server '${local.sql_server_name}' `
          --name $ipRuleName `
          --start-ip-address $currentIp `
          --end-ip-address $currentIp `
          --only-show-errors 2>&1

        if ($LASTEXITCODE -ne 0) {
          throw "Failed to create firewall rule. Azure CLI output: $firewallOutput"
        }

        Start-Sleep -Seconds 5

        $provisioningPrincipalName = '${data.azuread_service_principal.provisioning_principal.display_name}'
        $provisioningPrincipalObjectId = '${data.azuread_service_principal.provisioning_principal.object_id}'

        Write-Host "Ensuring SQL Entra admin is set to provisioning principal: $provisioningPrincipalName"
        $currentAdminObjectId = az sql server ad-admin list `
          --resource-group '${data.azurerm_resource_group.resource_group.name}' `
          --server '${local.sql_server_name}' `
          --query "[0].sid" `
          -o tsv `
          --only-show-errors 2>$null

        if ($LASTEXITCODE -ne 0) {
          throw "Failed to read current SQL Entra admin."
        }

        $currentAdminObjectId = "$currentAdminObjectId".Trim()
        if (-not $currentAdminObjectId -or $currentAdminObjectId -ne $provisioningPrincipalObjectId) {
          Write-Host "Setting SQL Entra admin to provisioning principal."
          $adminOutput = az sql server ad-admin create `
            --resource-group '${data.azurerm_resource_group.resource_group.name}' `
            --server '${local.sql_server_name}' `
            --display-name $provisioningPrincipalName `
            --object-id $provisioningPrincipalObjectId `
            --only-show-errors 2>&1

          if ($LASTEXITCODE -ne 0) {
            throw "Failed to set SQL Entra admin. Azure CLI output: $adminOutput"
          }

          # Allow Entra admin update to propagate before attempting SQL auth.
          Start-Sleep -Seconds 20
        }

        $users = ConvertFrom-Json '${jsonencode([for username in sort(keys(data.azuread_service_principal.database_users)) : {
    name      = username
    object_id = data.azuread_service_principal.database_users[username].object_id
}])}'

        $roles = ConvertFrom-Json '${jsonencode(local.db_permissions)}'

        Write-Host "Configuring $($users.Count) database users"
        $tokenOutput = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv 2>&1
        $token = "$tokenOutput".Trim()
        if ($LASTEXITCODE -ne 0 -or -not $token) {
          throw "Failed to acquire access token for SQL database. Azure CLI output: $tokenOutput"
        }

        foreach ($user in $users) {
          $userName = $user.name
          $objectId = $user.object_id

          $queryParts = @(
            "IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$userName') BEGIN CREATE USER [$userName] FROM EXTERNAL PROVIDER WITH OBJECT_ID = '$objectId'; END;",
            "ALTER USER [$userName] WITH DEFAULT_SCHEMA = [dbo];"
          )

          foreach ($role in $roles) {
            $queryParts += "IF NOT EXISTS (SELECT 1 FROM sys.database_role_members drm JOIN sys.database_principals r ON drm.role_principal_id = r.principal_id JOIN sys.database_principals m ON drm.member_principal_id = m.principal_id WHERE r.name = '$role' AND m.name = '$userName') BEGIN ALTER ROLE [$role] ADD MEMBER [$userName]; END;"
          }

          $queryParts += "GRANT EXECUTE TO [$userName];"
          $sql = $queryParts -join " "

          $maxAttempts = 8
          $attempt = 1
          while ($true) {
            try {
              Write-Host "Applying SQL permissions for user: $userName (attempt $attempt/$maxAttempts)"
              Invoke-Sqlcmd -ConnectionString '${local.connection_string_no_auth}' -AccessToken $token -Query $sql
              break
            }
            catch {
              $message = $_.Exception.Message
              $isTransient = $message -match "not currently available|temporarily unavailable|service is busy|timed out|timeout expired|transport-level error|error code 40613"
              if ($attempt -lt $maxAttempts -and $isTransient) {
                Write-Host "Transient SQL error for user $($userName): $message"
                Write-Host "Retrying in 15 seconds..."
                Start-Sleep -Seconds 15
                $attempt++
                continue
              }

              throw
            }
          }
        }

        Write-Host "Database users configured successfully"
      }
      catch {
        Write-Host "ERROR: $_"
        Write-Host $_.Exception.Message
        Write-Host $_.ScriptStackTrace
        throw
      }
      finally {
        $ErrorActionPreference = 'SilentlyContinue'
        if ($ipRuleName) {
          Write-Host "Removing temporary firewall rule: $ipRuleName"
          az sql server firewall-rule delete `
            --resource-group '${data.azurerm_resource_group.resource_group.name}' `
            --server '${local.sql_server_name}' `
            --name $ipRuleName `
            --yes `
            --only-show-errors `
            2>$null
        }
      }

      exit 0
    EOT

interpreter = ["pwsh", "-Command"]
}
}

module "backend_container_app" {
  source = "../modules/container_app"

  name                            = "gillytracker-${lower(local.environment)}-backend"
  container_app_environment_id    = data.azurerm_container_app_environment.existing.id
  resource_group_name             = data.azurerm_resource_group.resource_group.name
  identity_id                     = azurerm_user_assigned_identity.app_identity.id
  container_registry_login_server = data.azurerm_container_registry.existing.login_server

  env_vars = {
    AZURE_CLIENT_ID                       = azurerm_user_assigned_identity.app_identity.client_id
    KeyVault__VaultUri                    = azurerm_key_vault.app.vault_uri
    ConnectionStrings__Database           = local.database_connection_string
    APPLICATIONINSIGHTS_CONNECTION_STRING = module.application_insights.application_insights.connection_string
    Authentication__Microsoft__TenantId   = data.azurerm_client_config.current.tenant_id
    Authentication__Microsoft__ClientId   = azuread_application.backend_auth.client_id
    Authentication__Microsoft__ClientSecret = azuread_application_password.backend_auth.value
    Authentication__Microsoft__CallbackPath = "/signin-microsoft"
    Authorization__PetTrackerAdminsGroupObjectId = data.azuread_group.pet_tracker_admins.object_id
    # CORS: Allow the Static Web App origin and custom domains
    AllowedOrigins__0 = "https://${module.static_web_app.default_host_name}"
    AllowedOrigins__1 = "https://dogtracker.keboo.dev"
  }

  depends_on = [module.static_web_app]
}

module "static_web_app" {
  source = "../modules/static_web_app"

  name           = "gillytracker-${lower(local.environment)}-swa"
  resource_group = data.azurerm_resource_group.resource_group
  location       = "westus2"
  sku = {
    tier = "Free"
    size = "Free"
  }

  tags = local.tags
}

module "application_insights" {
  source = "../modules/app_insights"

  environment    = local.environment
  resource_group = data.azurerm_resource_group.resource_group
  tags           = local.tags

  reader_ids = {}
}
