output "resource_group_name" {
  value = azurerm_resource_group.main.name
}

output "api_app_name" {
  value = azurerm_linux_web_app.api.name
}

output "api_app_id" {
  value = azurerm_linux_web_app.api.id
}

output "api_url" {
  value = "https://${azurerm_linux_web_app.api.default_hostname}"
}

output "function_app_name" {
  value = azurerm_function_app_flex_consumption.notifications.name
}

output "static_web_app_name" {
  value = azurerm_static_web_app.web.name
}

output "static_web_app_url" {
  value = "https://${azurerm_static_web_app.web.default_host_name}"
}

output "static_web_app_api_key" {
  value     = azurerm_static_web_app.web.api_key
  sensitive = true
}

output "service_bus_namespace" {
  value = azurerm_servicebus_namespace.main.name
}

output "sql_server_name" {
  value = azurerm_mssql_server.main.name
}

output "sql_server_fqdn" {
  value = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "sql_admin_login" {
  value = var.sql_admin_login
}

output "sql_database_name" {
  value = azurerm_mssql_database.main.name
}

output "key_vault_name" {
  value = azurerm_key_vault.main.name
}

output "application_insights_name" {
  value = azurerm_application_insights.main.name
}
