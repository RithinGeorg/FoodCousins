variable "sql_admin_login" {
  type    = string
  default = "foodcousinsadmin"
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
  type    = string
  default = ""
}
