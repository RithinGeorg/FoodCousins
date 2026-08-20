namespace FoodCousins.Application.Storage;
public interface IImageStorage
{
    Task<string> UploadFoodImageAsync(Guid foodId, Stream content, string contentType, string fileName, CancellationToken cancellationToken);
}
