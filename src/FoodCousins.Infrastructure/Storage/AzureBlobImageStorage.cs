using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FoodCousins.Application.Storage;
using Microsoft.Extensions.Configuration;

namespace FoodCousins.Infrastructure.Storage;

public sealed class AzureBlobImageStorage : IImageStorage
{
    private readonly BlobContainerClient _container;
    public AzureBlobImageStorage(IConfiguration configuration)
    {
        var connectionString = configuration["Storage:ConnectionString"];
        var containerName = configuration["Storage:FoodImagesContainer"] ?? "food-images";
        if (!string.IsNullOrWhiteSpace(connectionString))
            _container = new BlobContainerClient(connectionString, containerName);
        else
        {
            var endpoint = configuration["Storage:BlobServiceUri"] ?? throw new InvalidOperationException("Storage:BlobServiceUri is required.");
            _container = new BlobContainerClient(new Uri($"{endpoint.TrimEnd('/')}/{containerName}"), new DefaultAzureCredential());
        }
    }

    public async Task<string> UploadFoodImageAsync(Guid foodId, Stream content, string contentType, string fileName, CancellationToken ct)
    {
        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(contentType)) throw new ArgumentException("Only JPEG, PNG and WebP images are supported.");
        await _container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10) extension = ".jpg";
        var blob = _container.GetBlobClient($"foods/{foodId:N}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}");
        await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, ct);
        return blob.Uri.ToString();
    }
}
