using System.Security.Cryptography;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace WattEngine.Flywheel.Flywheel;

public class FlywheelS3Configuration
{
    public string? ServiceUrl { get; set; }
    public string? Endpoint { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
}

public class FlywheelStorageConfiguration
{
    public string KeyPrefix { get; set; } = "flywheel";
    public long MaxBlobBytes { get; set; } = 512L * 1024 * 1024;
    public FlywheelS3Configuration S3 { get; set; } = new();
}

public class FlywheelBlobStorage(IConfiguration configuration, ILogger<FlywheelBlobStorage> logger)
{
    private readonly FlywheelStorageConfiguration _config = configuration.GetSection("Flywheel").Get<FlywheelStorageConfiguration>() ?? new();
    private readonly Lazy<IMinioClient> _s3 = new(() =>
    {
        var config = configuration.GetSection("Flywheel").Get<FlywheelStorageConfiguration>()?.S3 ?? new FlywheelS3Configuration();
        if (string.IsNullOrWhiteSpace(config.Bucket) || string.IsNullOrWhiteSpace(config.AccessKey) || string.IsNullOrWhiteSpace(config.SecretKey))
            throw new InvalidOperationException("Flywheel:S3 bucket and credentials are required.");
        var endpoint = config.Endpoint;
        var ssl = config.EnableSsl;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            if (string.IsNullOrWhiteSpace(config.ServiceUrl)) throw new InvalidOperationException("Flywheel:S3 endpoint or service URL is required.");
            var uri = new Uri(config.ServiceUrl);
            endpoint = uri.Authority;
            ssl = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        }
        var client = new MinioClient().WithEndpoint(endpoint).WithRegion(config.Region).WithCredentials(config.AccessKey, config.SecretKey);
        if (ssl) client = client.WithSSL();
        return client.Build();
    });

    public long MaxBlobBytes => _config.MaxBlobBytes;
    private string Bucket => _config.S3.Bucket;
    public string BuildObjectKey(Guid workspaceId, string appId, Guid blobId, long revision) =>
        $"{_config.KeyPrefix.Trim('/')}/{workspaceId:D}/{appId}/{blobId:D}/{revision}.bin";

    public async Task<(long Size, string Sha256)> SaveAsync(string key, IFormFile file, CancellationToken ct)
    {
        if (file.Length <= 0 || file.Length > MaxBlobBytes) throw new FlywheelValidationException($"Blob must be between 1 and {MaxBlobBytes} bytes.");
        string hash;
        await using (var hashStream = file.OpenReadStream()) { hash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, ct)).ToLowerInvariant(); }
        await using (var input = file.OpenReadStream())
            await _s3.Value.PutObjectAsync(new PutObjectArgs().WithBucket(Bucket).WithObject(key).WithStreamData(input).WithObjectSize(file.Length).WithContentType("application/octet-stream"), ct);
        return (file.Length, hash);
    }

    public async Task<Stream?> OpenAsync(string key, CancellationToken ct)
    {
        try
        {
            var memory = new MemoryStream();
            await _s3.Value.GetObjectAsync(new GetObjectArgs().WithBucket(Bucket).WithObject(key).WithCallbackStream(stream => stream.CopyTo(memory)), ct);
            memory.Position = 0;
            return memory;
        }
        catch (ObjectNotFoundException)
        {
            logger.LogWarning("Flywheel blob object missing. key={Key}", key);
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        try { await _s3.Value.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(Bucket).WithObject(key), ct); }
        catch (ObjectNotFoundException) { }
    }
}
