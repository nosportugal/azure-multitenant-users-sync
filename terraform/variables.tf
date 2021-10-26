variable "tenant_id" {
  type        = string
  description = "The azure tenant id where the function resides."
}

variable "subscription_id" {
  type        = string
  description = "The azure subscription id where the function will run."
}

variable "location" {
  type        = string
  description = "The Azure location where the function resources will be created."
}
