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

variable "GITHUB_ACTIONS_APP_CLIENT_ID" {
  description = "Client ID for the GillyTracker-GitHubActions service principal."
  type        = string
  default     = ""
}

variable "GITHUB_ACTIONS_INFRA_CLIENT_ID" {
  description = "Client ID for the GillyTracker-GitHubActionsInfra service principal."
  type        = string
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
