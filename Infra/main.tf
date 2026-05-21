locals {
  default_tags = {
    "app" = "GillyTracker"
  }

  database_users = {
    "GillyTracker-GitHubActions"      = var.GITHUB_ACTIONS_APP_OBJECT_ID
    "GillyTracker-GitHubActionsInfra" = var.GITHUB_ACTIONS_INFRA_OBJECT_ID
  }
}

module "prod" {
  source = "./prod"

  environment                  = var.environment
  location                     = var.location
  tags                         = local.default_tags
  existing_resource_group_name = var.existing_resource_group_name
  database_users               = local.database_users
}
