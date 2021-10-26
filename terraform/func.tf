locals {
  resource_group = "users-sync"
  storage_account = {
    name                      = "userssyncsa"
    account_tier              = "Standard"
    account_replication_type  = "LRS"
    min_tls_version           = "TLS1_2"
    enable_https_traffic_only = true
  }
  app_insights = {
    name             = "users-sync-insights"
    application_type = "other"
  }
  app_plan = {
    name     = "users-sync-app"
    kind     = "FunctionApp"
    reserved = true
    sku_tier = "Dynamic"
    sku_size = "Y1"
  }
}

resource "azurerm_resource_group" "default" {
  name     = local.resource_group
  location = var.location
}

resource "azurerm_storage_account" "this" {
  # checkov:skip=CKV_AZURE_35: Github actions whitelist limitation.
  # checkov:skip=CKV_AZURE_33: Queue properties don't apply @ the moment
  # checkov:skip=CKV2_AZURE_1: For now, the CMK's are disabled
  # checkov:skip=CKV2_AZURE_18: For now, the CMK's are disabled
  # checkov:skip=CKV2_AZURE_8: This applies to storage containers and not storage accounts.
  name                      = local.storage_account.name
  resource_group_name       = azurerm_resource_group.default.name
  location                  = var.location
  account_tier              = local.storage_account.account_tier
  account_replication_type  = local.storage_account.account_replication_type
  min_tls_version           = local.storage_account.min_tls_version
  enable_https_traffic_only = local.storage_account.enable_https_traffic_only

  network_rules {
    default_action = "Allow"
    #bypass         = ["AzureServices"]
  }

  identity {
    type = "SystemAssigned"
  }
}

resource "azurerm_application_insights" "this" {
  name                = local.app_insights.name
  location            = var.location
  resource_group_name = azurerm_resource_group.default.name
  application_type    = local.app_insights.application_type
}

resource "azurerm_app_service_plan" "this" {
  name                = local.app_plan.name
  resource_group_name = azurerm_resource_group.default.name
  location            = var.location
  kind                = local.app_plan.kind
  reserved            = local.app_plan.reserved # this has to be set to true for Linux. Not related to the Premium Plan
  sku {
    tier = local.app_plan.sku_tier
    size = local.app_plan.sku_size
  }
}

resource "azurerm_function_app" "this" {
  name                       = "users-sync"
  resource_group_name        = azurerm_resource_group.default.name
  location                   = var.location
  app_service_plan_id        = azurerm_app_service_plan.this.id
  os_type                    = "linux"
  storage_account_name       = azurerm_storage_account.this.name
  storage_account_access_key = azurerm_storage_account.this.primary_access_key
  version                    = "~3"
  https_only                 = true

  app_settings = {
    "ScheduleTrigger"                = "0 0 * * * *"
    "FUNCTIONS_WORKER_RUNTIME"       = "dotnet",
    "APPINSIGHTS_INSTRUMENTATIONKEY" = azurerm_application_insights.this.instrumentation_key,
    "SRC_TENANT_ID"                  = "<THE_SOURCE_TENANT_ID>",
    "SRC_GROUP_ID"                   = "<THE_SOURCE_GROUP_ID>",
    "DST_TENANT_ID"                  = "<THE_DESTINATION_TENANT_ID>",
    "DST_GROUP_ID"                   = "<THE_DESTINATION_GROUP_ID>",
    "CLIENT_ID"                      = "<THE_APP_REGISTRATION_ID>",
    "CLIENT_SECRET"                  = "@Microsoft.KeyVault(SecretUri=<URI_TO_THE_APP_REGISTRATION_SECRET>",
    "INVITE_BASE_URL"                = "https://portal.azure.com"
    "REQUEST_MAX_RETRIES"            = "5"
  }

  site_config {
    http2_enabled             = true
    linux_fx_version          = "dotnet|3.1"
    use_32_bit_worker_process = false
  }

  identity {
    type = "SystemAssigned"
  }

  auth_settings {
    enabled = true
  }
}
