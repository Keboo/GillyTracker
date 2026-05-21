locals {
  default_tags = {
    "app" = "GillyTracker"
  }

  # Exclude empty object IDs so SQL user provisioning does not run invalid CREATE USER statements.
  database_users = {
    for user_name, object_id in {
      "GillyTracker-GitHubActions"      = var.GITHUB_ACTIONS_APP_OBJECT_ID
      "GillyTracker-GitHubActionsInfra" = var.GITHUB_ACTIONS_INFRA_OBJECT_ID
    } : user_name => trimspace(object_id)
    if trimspace(object_id) != ""
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
