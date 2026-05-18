locals {
  environment = var.environment
  tags = merge(var.tags,
    {
      "Environment" = local.environment
  })
}

resource "azurerm_resource_group" "resource_group" {
  name     = var.existing_resource_group_name
  location = var.location
  tags     = local.tags
}

resource "azurerm_user_assigned_identity" "app_identity" {
  name                = "gillytracker-${lower(local.environment)}-mi"
  location            = azurerm_resource_group.resource_group.location
  resource_group_name = azurerm_resource_group.resource_group.name

  tags = local.tags
}

data "azurerm_container_app_environment" "existing" {
  name                = var.existing_container_app_environment_name
  resource_group_name = azurerm_resource_group.resource_group.name
}

module "backend_container_app" {
  source = "../modules/container_app"

  name                            = "gillytracker-${lower(local.environment)}-backend"
  container_app_environment_id    = data.azurerm_container_app_environment.existing.id
  resource_group_name             = azurerm_resource_group.resource_group.name
  identity_id                     = azurerm_user_assigned_identity.app_identity.id
  container_registry_login_server = var.acr_login_server

  env_vars = {
    AZURE_CLIENT_ID = azurerm_user_assigned_identity.app_identity.client_id
    ConnectionStrings__Database = var.database_connection_string
    APPLICATIONINSIGHTS_CONNECTION_STRING = module.application_insights.application_insights.connection_string
  }
}

module "application_insights" {
  source = "../modules/app_insights"

  environment    = local.environment
  resource_group = azurerm_resource_group.resource_group
  tags           = local.tags

  reader_ids = {}
}
