module "platform" {
  source = "../../modules/platform"

  environment                = "dev"
  location                   = "Australia East"
  function_location          = "Southeast Asia"
  static_web_app_location    = "East Asia"
  sql_admin_login            = var.sql_admin_login
  sql_admin_password         = var.sql_admin_password
  alert_email                = var.alert_email
  monthly_budget             = var.monthly_budget
  budget_start_date          = var.budget_start_date
  additional_frontend_origin = var.additional_frontend_origin

  tags = {
    owner = "FoodCousins"
  }
}

output "resource_group_name" {
  value = module.platform.resource_group_name
}

output "api_app_name" {
  value = module.platform.api_app_name
}

output "api_app_id" {
  value = module.platform.api_app_id
}

output "api_url" {
  value = module.platform.api_url
}

output "function_app_name" {
  value = module.platform.function_app_name
}

output "static_web_app_url" {
  value = module.platform.static_web_app_url
}

output "static_web_app_api_key" {
  value     = module.platform.static_web_app_api_key
  sensitive = true
}

output "key_vault_name" {
  value = module.platform.key_vault_name
}

output "sql_server_fqdn" {
  value = module.platform.sql_server_fqdn
}

output "sql_database_name" {
  value = module.platform.sql_database_name
}

output "sql_admin_login" {
  value = module.platform.sql_admin_login
}
