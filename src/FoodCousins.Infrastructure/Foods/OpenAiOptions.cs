namespace FoodCousins.Infrastructure.Foods;

public sealed class OpenAiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-5.6-luna";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public int MaxOutputTokens { get; set; } = 9000;
}
