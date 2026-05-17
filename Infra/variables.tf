variable "CLIENT_ID" {
  description = "Value of the client id of the service principal"
  type        = string
  default     = ""
}

variable "TENANT_ID" {
  type        = string
  description = "Value of the tenant id of the service principal"
  default     = ""
}

variable "SUBSCRIPTION_ID" {
  type        = string
  description = "Value of the subscription id to use"
  default     = ""
}

variable "location" {
  description = "Azure region for the deployment resources."
  type        = string
  default     = "westus2"
}

variable "environment" {
  description = "Deployment environment name."
  type        = string
  default     = "dev"
}

variable "existing_resource_group_name" {
  description = "Existing resource group that contains shared infrastructure."
  type        = string
  default     = "KebooDev"
}

variable "existing_container_app_environment_name" {
  description = "Existing Azure Container App Environment name."
  type        = string
  default     = "keboodev-env"
}

variable "acr_login_server" {
  description = "Existing Azure Container Registry login server."
  type        = string
  default     = "keboodevacr.azurecr.io"
}

variable "database_connection_string" {
  description = "Connection string for the existing SQL database."
  type        = string
  sensitive   = true
}
