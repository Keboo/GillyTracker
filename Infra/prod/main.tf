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
  db_permissions = [
    "db_datareader",
    "db_datawriter",
    "db_ddladmin"
  ]
}

data "azurerm_resource_group" "resource_group" {
  name = var.existing_resource_group_name
}

resource "azurerm_user_assigned_identity" "app_identity" {
  name                = "gillytracker-${lower(local.environment)}-mi"
  location            = data.azurerm_resource_group.resource_group.location
  resource_group_name = data.azurerm_resource_group.resource_group.name

  tags = local.tags
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

resource "terraform_data" "setup_database_user" {
  for_each = var.database_users

  triggers_replace = [
    data.azurerm_mssql_database.existing.id,
    each.key,
    each.value,
    join(",", local.db_permissions),
    "v1"
  ]

  provisioner "local-exec" {
    command = <<-EOT
      try {
        $currentIp = (Invoke-RestMethod -Uri "https://api.ipify.org").ToString()
        $ipRuleName = 'TerraformTemp-AllowCurrentIP'
        $azureRuleName = 'AllowAllWindowsAzureIps'

        Write-Host "Installing SqlServer module..."
        Install-Module -Name SqlServer -AcceptLicense -Force -ErrorAction SilentlyContinue
        Import-Module SqlServer -ErrorAction Stop
        Write-Host "SqlServer module loaded successfully"

        $ErrorActionPreference = 'Stop'

        Write-Host "Creating temporary firewall rule for IP: $currentIp"
        az sql server firewall-rule create `
          --resource-group '${data.azurerm_resource_group.resource_group.name}' `
          --server '${local.sql_server_name}' `
          --name $ipRuleName `
          --start-ip-address $currentIp `
          --end-ip-address $currentIp `
          --output none

        Write-Host "Enabling 'Allow Azure services' firewall rule"
        az sql server firewall-rule create `
          --resource-group '${data.azurerm_resource_group.resource_group.name}' `
          --server '${local.sql_server_name}' `
          --name $azureRuleName `
          --start-ip-address '0.0.0.0' `
          --end-ip-address '0.0.0.0' `
          --output none

        if ($LASTEXITCODE -ne 0) {
          throw "Failed to create firewall rule"
        }

        Start-Sleep -Seconds 5

        $sql = @"
        IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = '${each.key}')
        BEGIN
          CREATE USER [${each.key}] FROM EXTERNAL PROVIDER WITH OBJECT_ID = '${each.value}';
        END;

        ALTER USER [${each.key}] WITH DEFAULT_SCHEMA = [dbo];

        ${join("\n", [for role in local.db_permissions : "ALTER ROLE ${role} ADD MEMBER [${each.key}];"])}
        GRANT EXECUTE TO [${each.key}];
        "@

        Write-Host "Configuring database user '${each.key}' for principal ID '${each.value}'"
        $token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
        if ($LASTEXITCODE -ne 0 -or -not $token) {
          throw "Failed to acquire access token for SQL database"
        }

        Invoke-Sqlcmd -ConnectionString '${local.connection_string_no_auth}' -AccessToken $token -Query $sql
        Write-Host "Database user configured successfully"
      }
      catch {
        Write-Host "ERROR: $_"
        Write-Host $_.Exception.Message
        Write-Host $_.ScriptStackTrace
        throw
      }
      finally {
        Write-Host "Removing temporary firewall rules"
        $ErrorActionPreference = 'SilentlyContinue'
        az sql server firewall-rule delete `
          --resource-group '${data.azurerm_resource_group.resource_group.name}' `
          --server '${local.sql_server_name}' `
          --name $ipRuleName `
          --yes `
          2>$null
        az sql server firewall-rule delete `
          --resource-group '${data.azurerm_resource_group.resource_group.name}' `
          --server '${local.sql_server_name}' `
          --name $azureRuleName `
          --yes `
          2>$null
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
    ConnectionStrings__Database           = local.database_connection_string
    APPLICATIONINSIGHTS_CONNECTION_STRING = module.application_insights.application_insights.connection_string
    # CORS: Allow the Static Web App origin
    AllowedOrigins__0 = "https://${module.static_web_app.default_host_name}"
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
