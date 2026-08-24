namespace FoodCousins.Infrastructure.Email;

public sealed class EmailOptions
{
    public string Provider { get; set; } = "Logging";
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@foodcousins.local";
    public string FromName { get; set; } = "FoodCousins";
}
