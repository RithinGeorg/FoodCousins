data "azurerm_client_config" "current" {}

resource "random_string" "suffix" {
  length  = 6
  upper   = false
  special = false
}

locals {
  prefix = "foodcousins-${var.environment}"
  suffix = random_string.suffix.result
  tags = merge(var.tags, { application = "FoodCousins", environment = var.environment, managed_by = "Terraform" })
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.prefix}"
  location = var.location
  tags     = local.tags
}

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${local.prefix}-${local.suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  daily_quota_gb      = 0.5
  tags                = local.tags
}

resource "azurerm_application_insights" "main" {
  name                = "appi-${local.prefix}-${local.suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = local.tags
}

resource "azurerm_static_web_app" "web" {
  name                          = "swa-${local.prefix}-${local.suffix}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = var.static_web_app_location
  sku_tier                      = "Free"
  sku_size                      = "Free"
  preview_environments_enabled  = true
  public_network_access_enabled = true
  tags                          = local.tags
  lifecycle {
    ignore_changes = [repository_branch, repository_url]
  }
}

resource "azurerm_service_plan" "api" {
  name                = "asp-${local.prefix}-${local.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = "B1"
  tags                = local.tags
}

resource "azurerm_mssql_server" "main" {
  name                         = "sql-${local.prefix}-${local.suffix}"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password
  minimum_tls_version          = "1.2"
  tags                         = local.tags
}

resource "azurerm_mssql_firewall_rule" "allow_azure" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_mssql_database" "main" {
  name         = "sqldb-${local.prefix}"
  server_id    = azurerm_mssql_server.main.id
  sku_name     = "Basic"
  max_size_gb  = 2
  zone_redundant = false
  tags         = local.tags
}

resource "azurerm_storage_account" "main" {
  name                            = "stfc${var.environment}${local.suffix}"
  resource_group_name             = azurerm_resource_group.main.name
  location                        = azurerm_resource_group.main.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = true
  shared_access_key_enabled       = true
  tags                            = local.tags
}

resource "azurerm_storage_container" "food_images" {
  name                  = "food-images"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "blob"
}

resource "azurerm_storage_account" "functions" {
  name                            = "stfcfunc${var.environment}${local.suffix}"
  resource_group_name             = azurerm_resource_group.main.name
  location                        = var.function_location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = true
  tags                            = local.tags
}

resource "azurerm_storage_container" "function_deployments" {
  name                  = "function-deployments"
  storage_account_id    = azurerm_storage_account.functions.id
  container_access_type = "private"
}

resource "azurerm_key_vault" "main" {
  name                       = "kv-fc-${var.environment}-${local.suffix}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true
  soft_delete_retention_days = 7
  purge_protection_enabled   = false
  tags                       = local.tags
}

resource "azurerm_servicebus_namespace" "main" {
  name                = "sb-${local.prefix}-${local.suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "Basic"
  minimum_tls_version = "1.2"
  tags                = local.tags
}

resource "azurerm_servicebus_queue" "order_processing" {
  name                                 = "order-processing"
  namespace_id                         = azurerm_servicebus_namespace.main.id
  max_delivery_count                   = 10
  dead_lettering_on_message_expiration = true
  default_message_ttl                  = "P7D"
}

resource "azurerm_servicebus_queue" "notifications" {
  name                                 = "notifications"
  namespace_id                         = azurerm_servicebus_namespace.main.id
  max_delivery_count                   = 10
  dead_lettering_on_message_expiration = true
  default_message_ttl                  = "P7D"
}

resource "azurerm_linux_web_app" "api" {
  name                = "app-fc-api-${var.environment}-${local.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_service_plan.api.location
  service_plan_id     = azurerm_service_plan.api.id
  https_only          = true
  tags                = local.tags

  identity {
    type = "SystemAssigned"
  }

  site_config {
    always_on              = true
    health_check_path                 = "/health"
    health_check_eviction_time_in_min = 10
    minimum_tls_version    = "1.2"
    scm_minimum_tls_version = "1.2"
    http2_enabled          = true
    application_stack {
      dotnet_version = "10.0"
    }
  }

  app_settings = merge(
    {
      "ASPNETCORE_ENVIRONMENT"                = "Development"
      "WEBSITE_RUN_FROM_PACKAGE"              = "1"
      "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.main.connection_string
      "ConnectionStrings__FoodCousins"        = "@Microsoft.KeyVault(VaultName=${azurerm_key_vault.main.name};SecretName=sql-connection-string)"
      "Jwt__Issuer"                           = "FoodCousins"
      "Jwt__Audience"                         = "FoodCousins"
      "Jwt__SigningKey"                       = "@Microsoft.KeyVault(VaultName=${azurerm_key_vault.main.name};SecretName=jwt-signing-key)"
      "Jwt__AccessTokenMinutes"               = "15"
      "Jwt__RefreshTokenDays"                 = "7"
      "Cors__AllowedOrigins__0"               = "https://${azurerm_static_web_app.web.default_host_name}"
      "Auth__CrossSiteCookies"                = "true"
      "Auth__AllowInsecureCookies"            = "false"
      "Database__EnsureCreated"               = "true"
      "Storage__BlobServiceUri"               = azurerm_storage_account.main.primary_blob_endpoint
      "Storage__FoodImagesContainer"          = azurerm_storage_container.food_images.name
      "ServiceBus__FullyQualifiedNamespace"   = "${azurerm_servicebus_namespace.main.name}.servicebus.windows.net"
      "ServiceBus__OrderProcessingQueue"      = azurerm_servicebus_queue.order_processing.name
      "ServiceBus__NotificationsQueue"        = azurerm_servicebus_queue.notifications.name
    },
    var.additional_frontend_origin == "" ? {} : {
      "Cors__AllowedOrigins__1" = var.additional_frontend_origin
    }
  )
}

resource "azurerm_role_assignment" "api_blob" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_web_app.api.identity[0].principal_id
}
resource "azurerm_role_assignment" "api_servicebus_sender" {
  scope                = azurerm_servicebus_namespace.main.id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = azurerm_linux_web_app.api.identity[0].principal_id
}
resource "azurerm_role_assignment" "api_keyvault" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app.api.identity[0].principal_id
}

resource "azurerm_role_assignment" "deployer_keyvault" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_service_plan" "functions" {
  name                = "asp-func-${local.prefix}-${local.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.function_location
  os_type             = "Linux"
  sku_name            = "FC1"
  tags                = local.tags
}

resource "azurerm_function_app_flex_consumption" "notifications" {
  name                = "func-fc-${var.environment}-${local.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.function_location
  service_plan_id     = azurerm_service_plan.functions.id
  storage_container_type      = "blobContainer"
  storage_container_endpoint  = "${azurerm_storage_account.functions.primary_blob_endpoint}${azurerm_storage_container.function_deployments.name}"
  storage_authentication_type = "StorageAccountConnectionString"
  storage_access_key          = azurerm_storage_account.functions.primary_access_key
  runtime_name                = "dotnet-isolated"
  runtime_version             = "10.0"
  maximum_instance_count      = 2
  instance_memory_in_mb       = 2048
  https_only                  = true
  tags                        = local.tags

  identity {
    type = "SystemAssigned"
  }
  site_config {
    application_insights_connection_string = azurerm_application_insights.main.connection_string
    minimum_tls_version                     = "1.2"
  }
  app_settings = {
    "ServiceBusConnection__fullyQualifiedNamespace" = "${azurerm_servicebus_namespace.main.name}.servicebus.windows.net"
    "ConnectionStrings__FoodCousins"                = "@Microsoft.KeyVault(VaultName=${azurerm_key_vault.main.name};SecretName=sql-connection-string)"
    "APPLICATIONINSIGHTS_CONNECTION_STRING"         = azurerm_application_insights.main.connection_string
  }
}

resource "azurerm_role_assignment" "function_servicebus_receiver" {
  scope                = azurerm_servicebus_namespace.main.id
  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = azurerm_function_app_flex_consumption.notifications.identity[0].principal_id
}

resource "azurerm_role_assignment" "function_keyvault" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_function_app_flex_consumption.notifications.identity[0].principal_id
}

resource "azurerm_monitor_action_group" "dev" {
  name                = "ag-${local.prefix}"
  resource_group_name = azurerm_resource_group.main.name
  short_name          = "fcdev"
  email_receiver {
    name          = "FoodCousins owner"
    email_address = var.alert_email
  }

  tags = local.tags
}

resource "azurerm_monitor_metric_alert" "api_5xx" {
  name                = "alert-${local.prefix}-api-5xx"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_linux_web_app.api.id]
  description         = "FoodCousins Dev API returned more than five HTTP 5xx responses in five minutes."
  severity            = 2
  frequency           = "PT1M"
  window_size         = "PT5M"
  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "Http5xx"
    aggregation      = "Total"
    operator         = "GreaterThan"
    threshold        = 5
  }
  action {
    action_group_id = azurerm_monitor_action_group.dev.id
  }

  tags = local.tags
}

resource "azurerm_consumption_budget_resource_group" "dev" {
  name              = "budget-${local.prefix}"
  resource_group_id = azurerm_resource_group.main.id
  amount            = var.monthly_budget
  time_grain        = "Monthly"
  time_period {
    start_date = var.budget_start_date
  }
  notification {
    enabled        = true
    threshold      = 80
    operator       = "GreaterThanOrEqualTo"
    threshold_type = "Actual"
    contact_emails = [var.alert_email]
  }
  notification {
    enabled        = true
    threshold      = 100
    operator       = "GreaterThanOrEqualTo"
    threshold_type = "Forecasted"
    contact_emails = [var.alert_email]
  }
}
