locals {
  default_tags = {
    "app" = "GillyTracker"
  }
}

module "prod" {
  source = "./prod"

  environment                         = var.environment
  location                            = var.location
  tags                                = local.default_tags
  acr_login_server                    = var.acr_login_server
  database_connection_string          = var.database_connection_string
  existing_resource_group_name        = var.existing_resource_group_name
  existing_container_app_environment_name = var.existing_container_app_environment_name
}
