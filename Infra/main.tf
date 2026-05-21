locals {
  default_tags = {
    "app" = "GillyTracker"
  }

  database_users = {
    "GillyTracker-GitHubActions"      = data.azuread_service_principal.github_actions_app.object_id
    "GillyTracker-GitHubActionsInfra" = data.azuread_service_principal.github_actions_infra.object_id
  }
}

data "azuread_service_principal" "github_actions_app" {
  display_name = "GillyTracker-GitHubActions"
}

data "azuread_service_principal" "github_actions_infra" {
  display_name = "GillyTracker-GitHubActionsInfra"
}

module "prod" {
  source = "./prod"

  environment                  = var.environment
  location                     = var.location
  tags                         = local.default_tags
  existing_resource_group_name = var.existing_resource_group_name
  database_users               = local.database_users
}
