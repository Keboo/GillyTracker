locals {
  environment = var.environment
  tags = merge(var.tags,
    {
      "Environment" = local.environment
  })
  sql_server_name   = "keboodev-sql"
  sql_database_name = "keboodevdb"

  database_connection_string = "Server=tcp:${data.azurerm_mssql_server.existing.fully_qualified_domain_name},1433;Initial Catalog=${data.azurerm_mssql_database.existing.name};Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;Authentication=\"Active Directory Default\";"
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
  }
}

module "application_insights" {
  source = "../modules/app_insights"

  environment    = local.environment
  resource_group = data.azurerm_resource_group.resource_group
  tags           = local.tags

  reader_ids = {}
}
