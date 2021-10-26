variable "tenant_id" {
  type        = string
  description = "The azure tenant id."
}

variable "subscription_id" {
  type        = string
  description = "The azure subscription id where the function will run."
}

variable "location" {
  type        = string
  description = "The resouces Azure location."
}
