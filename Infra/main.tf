locals {
  default_tags = {
    "app" = "GillyTracker"
  }

  # Map service principal client IDs to usernames for provisioning
  database_users_client_ids = {
    "GillyTracker-GitHubActions"      = var.GITHUB_ACTIONS_APP_CLIENT_ID
    "GillyTracker-GitHubActionsInfra" = var.GITHUB_ACTIONS_INFRA_CLIENT_ID
  }
}

module "prod" {
  source = "./prod"

  environment                  = var.environment
  location                     = var.location
  tags                         = local.default_tags
  existing_resource_group_name = var.existing_resource_group_name
  database_users_client_ids    = local.database_users_client_ids
}
