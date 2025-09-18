using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

public class BlobService
{
    private readonly BlobContainerClient _containerClient;

    public BlobService(IConfiguration config)
    {

        var connectionStringTemplate = config["AzureStorage:ConnectionString"];

        var key = config["AzureStorage:AccountKey"];


        var connectionString = connectionStringTemplate.Replace("{AzureStorageKey}", key);

        var containerName = config["AzureStorage:ContainerName"];

        _containerClient = new BlobContainerClient(connectionString, containerName);

        _containerClient.CreateIfNotExists();
        _containerClient.SetAccessPolicy(PublicAccessType.Blob);
    }

    // new “test-friendly” ctor
    public BlobService(BlobContainerClient containerClient)
    {
        _containerClient = containerClient
            ?? throw new ArgumentNullException(nameof(containerClient));
    }

    public virtual async Task<string> UploadAsync(IFormFile file)
    {

        var safeFileName = Path.GetFileNameWithoutExtension(file.FileName)
            .Replace(" ", "_")
            .Replace("#", "")
            .Replace("%", "")
            .Replace("&", "")
            + Path.GetExtension(file.FileName);

        var blobName = Guid.NewGuid().ToString() + "_" + safeFileName;
        var blobClient = _containerClient.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders
        {
            ContentType = file.ContentType
        };

        using (var stream = file.OpenReadStream())
        {
            await blobClient.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = headers
            });
        }

        return blobClient.Uri.ToString();
    }
}
