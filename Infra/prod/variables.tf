variable "environment" {
  description = "The deployment environment (e.g., Dev, Prod)"
  type        = string
}

variable "existing_resource_group_name" {
  description = "Existing resource group where infrastructure already exists."
  type        = string
}

variable "location" {
  description = "Azure region for the resources"
  type        = string
}

variable "tags" {
  description = "Tags to apply to all resources"
  type        = map(string)
  default     = {}
}

variable "database_users_client_ids" {
  description = "Map of SQL database users keyed by user name with service principal client ID as value."
  type        = map(string)
  default     = {}
}
