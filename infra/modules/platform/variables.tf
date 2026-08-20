variable "environment" {
  type = string
}

variable "location" {
  type = string
}

variable "function_location" {
  type = string
}

variable "static_web_app_location" {
  type = string
}

variable "sql_admin_login" {
  type = string
}

variable "sql_admin_password" {
  type      = string
  sensitive = true
}

variable "alert_email" {
  type = string
}

variable "monthly_budget" {
  type    = number
  default = 75
}

variable "budget_start_date" {
  type    = string
  default = "2026-08-01T00:00:00Z"
}

variable "additional_frontend_origin" {
  description = "Optional extra frontend origin, usually a custom Dev domain such as https://dev.foodcousins.com."
  type        = string
  default     = ""
}

variable "tags" {
  type    = map(string)
  default = {}
}
