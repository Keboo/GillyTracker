locals {
  default_tags = {
    "app" = "GillyTracker"
  }
}

module "prod" {
  source = "./prod"

  environment                  = var.environment
  location                     = var.location
  tags                         = local.default_tags
  existing_resource_group_name = var.existing_resource_group_name
}
