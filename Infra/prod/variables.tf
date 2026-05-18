variable "acr_login_server" {
  description = "The login server of the container registry."
  type        = string
}

variable "environment" {
  description = "The deployment environment (e.g., Dev, Prod)"
  type        = string
}

variable "database_connection_string" {
  description = "Connection string for the existing SQL database."
  type        = string
  sensitive   = true
}

variable "existing_resource_group_name" {
  description = "Existing resource group where infrastructure already exists."
  type        = string
}

variable "existing_container_app_environment_name" {
  description = "Existing container app environment name."
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
