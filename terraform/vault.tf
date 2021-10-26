locals {
  rg = {
    name     = "users-sync-vault"
    location = "westeurope"
  }
  kv = {
    name             = "users-sync-kv"
    disk_protection  = true
    retention_days   = 7
    purge_protection = true
    sku              = "standard"
  }
}

resource "azurerm_resource_group" "key_vault" {
  name     = local.rg.name
  location = local.rg.location
}

resource "azurerm_key_vault" "default" {
  name                        = local.kv.name
  resource_group_name         = azurerm_resource_group.key_vault.name
  location                    = azurerm_resource_group.key_vault.location
  enabled_for_disk_encryption = local.kv.disk_protection
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  soft_delete_retention_days  = local.kv.retention_days
  purge_protection_enabled    = local.kv.purge_protection
  sku_name                    = local.kv.sku

  network_acls {
    default_action = "Deny"
    bypass         = "AzureServices"
    ip_rules       = flatten(split(",", azurerm_function_app.this.outbound_ip_addresses))
  }

  access_policy {
    tenant_id = data.azurerm_client_config.current.tenant_id
    object_id = azurerm_function_app.this.identity.0.principal_id

    key_permissions = [
      "Get",
      "List"
    ]

    secret_permissions = [
      "Get",
      "List"
    ]

    storage_permissions = [
      "Get",
      "List"
    ]
  }

  lifecycle {
    prevent_destroy = true
  }
}

