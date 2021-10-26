# We strongly recommend using the required_providers block to set the
# Azure Provider source and version being used
terraform {
  required_version = ">= 1.0.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~>2.77.0"
    }
  }

  # Configure the terraform backend if you want to store the tfstate remotely
  # backend "azurerm" {}
}

# Configure the Microsoft Azure Provider
provider "azurerm" {
  ## the following values are stored as env vars
  #client_id = ""
  #client_secret = ""
  tenant_id       = var.tenant_id
  subscription_id = var.subscription_id
  features {}
}

# Data resource to access the current azurerm client configuration
data "azurerm_client_config" "current" {}
