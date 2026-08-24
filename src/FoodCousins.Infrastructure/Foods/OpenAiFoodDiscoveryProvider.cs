using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FoodCousins.Application.Foods;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FoodCousins.Infrastructure.Foods;

public sealed class OpenAiFoodDiscoveryProvider(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    ILogger<OpenAiFoodDiscoveryProvider> logger) : IFoodDiscoveryProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly OpenAiOptions _options = options.Value;

    public string ProviderName => "OpenAI";
    public string ModelName => _options.Model;

    public async Task<AiFoodDiscoveryContract> DiscoverAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException(
                "OpenAI:ApiKey is not configured. FoodCousins only calls AI after a database miss.");

        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Food search query is required.", nameof(query));

        var requestBody = new
        {
            model = _options.Model,
            input = BuildPrompt(query.Trim()),
            max_output_tokens = _options.MaxOutputTokens
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(EnsureTrailingSlash(_options.BaseUrl)), "responses"));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        logger.LogInformation(
            "Food discovery database miss. Calling provider {Provider} model {Model} for query {Query}.",
            ProviderName,
            ModelName,
            query);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Food discovery provider returned HTTP {StatusCode}. Query={Query}.",
                (int)response.StatusCode,
                query);

            throw new InvalidOperationException(
                $"Food discovery provider failed with HTTP {(int)response.StatusCode}.");
        }

        using var responseJson = JsonDocument.Parse(raw);
        var outputText = ExtractOutputText(responseJson.RootElement);
        var cleanJson = StripMarkdownCodeFence(outputText);

        AiFoodDiscoveryContract? result;
        try
        {
            result = JsonSerializer.Deserialize<AiFoodDiscoveryContract>(cleanJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Food discovery provider returned invalid JSON. Query={Query}.", query);
            throw new InvalidOperationException("Food discovery provider returned invalid structured food data.", ex);
        }

        if (result is null)
            throw new InvalidOperationException("Food discovery provider returned no food data.");

        Validate(result);
        return result;
    }

    private static string BuildPrompt(string query) => $$"""
        You are the food-discovery engine for FoodCousins.

        User searched for: "{{query}}"

        Return ONLY one valid JSON object. Do not use markdown fences and do not add commentary.
        The object must use this exact shape:
        {
          "food": {
            "name": "string",
            "description": "string",
            "cuisine": "string",
            "countryOrRegion": "string or null",
            "tags": "comma-separated short tags",
            "recipe": {
              "title": "string",
              "prepMinutes": 20,
              "cookMinutes": 40,
              "servings": 4,
              "steps": ["step 1", "step 2"]
            },
            "ingredients": [
              {
                "name": "Rice",
                "quantityPerServing": 100,
                "unit": "g",
                "isOptional": false,
                "notes": null
              }
            ]
          },
          "cousins": [
            {
              "food": { SAME FOOD SHAPE AS ABOVE },
              "similarity": {
                "overall": 82,
                "taste": 86,
                "texture": 80,
                "ingredients": 88,
                "cookingMethod": 90,
                "dishType": 92,
                "cuisine": 55,
                "dietary": 100,
                "whySimilar": "short human-readable explanation"
              }
            }
          ]
        }

        Requirements:
        - Return the searched food plus 5 genuinely useful food cousins.
        - Include both same-cuisine and cross-culture cousins when appropriate.
        - All similarity scores are numbers from 0 to 100.
        - Give realistic recipe ingredients and cooking steps.
        - Quantities are PER SERVING so FoodCousins can scale an ingredient kit by people count.
        - Do not invent allergens or health claims.
        - Keep descriptions factual and concise.
        """;

    private static string ExtractOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
            return outputText.GetString() ?? string.Empty;

        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var contentItem in content.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                        return text.GetString() ?? string.Empty;
                }
            }
        }

        throw new InvalidOperationException("Food discovery provider response contained no output text.");
    }

    private static string StripMarkdownCodeFence(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;

        var firstNewLine = trimmed.IndexOf('\n');
        if (firstNewLine < 0) return trimmed.Trim('`');

        var body = trimmed[(firstNewLine + 1)..];
        var lastFence = body.LastIndexOf("```", StringComparison.Ordinal);
        return lastFence >= 0 ? body[..lastFence].Trim() : body.Trim();
    }

    private static void Validate(AiFoodDiscoveryContract result)
    {
        ValidateFood(result.Food, "food");

        if (result.Cousins.Count is < 1 or > 10)
            throw new InvalidOperationException("Food discovery must return between 1 and 10 cousins.");

        foreach (var cousin in result.Cousins)
        {
            ValidateFood(cousin.Food, "cousin");
            ValidateScore(cousin.Similarity.Overall);
            ValidateScore(cousin.Similarity.Taste);
            ValidateScore(cousin.Similarity.Texture);
            ValidateScore(cousin.Similarity.Ingredients);
            ValidateScore(cousin.Similarity.CookingMethod);
            ValidateScore(cousin.Similarity.DishType);
            ValidateScore(cousin.Similarity.Cuisine);
            ValidateScore(cousin.Similarity.Dietary);

            if (string.IsNullOrWhiteSpace(cousin.Similarity.WhySimilar))
                throw new InvalidOperationException("A cousin is missing its similarity explanation.");
        }
    }

    private static void ValidateFood(AiFoodContract food, string label)
    {
        if (string.IsNullOrWhiteSpace(food.Name) || string.IsNullOrWhiteSpace(food.Cuisine))
            throw new InvalidOperationException($"AI {label} is missing a name or cuisine.");
        if (food.Recipe.Steps.Count == 0 || food.Ingredients.Count == 0)
            throw new InvalidOperationException($"AI {label} is missing recipe steps or ingredients.");
    }

    private static void ValidateScore(decimal score)
    {
        if (score is < 0 or > 100)
            throw new InvalidOperationException("AI similarity scores must be between 0 and 100.");
    }

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
}
