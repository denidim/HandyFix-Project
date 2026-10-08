namespace HandyFix.Services
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using Amazon.S3;
    using Amazon.S3.Model;
    using Microsoft.Extensions.Configuration;

    public class CloudflareR2Service : ICloudflareR2Service
    {
        private readonly IConfiguration configuration;

        public CloudflareR2Service(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        // The key of the object a saved address points at, or null when the address is not one in
        // the bucket. UploadFileAsync builds an address as the bucket's public address followed by
        // the key, so the key is what is left after it. A photo saved while the bucket had another
        // public address still has its key as its path.
        public static string GetObjectKey(string publicUrl, string fileUrl)
        {
            if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out Uri address)
                || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
            {
                return null;
            }

            var prefix = (publicUrl ?? string.Empty).TrimEnd('/') + "/";
            if (prefix.Length > 1 && fileUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return fileUrl.Substring(prefix.Length);
            }

            return Uri.UnescapeDataString(address.AbsolutePath).TrimStart('/');
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder)
        {
            using (AmazonS3Client client = this.CreateClient(out var bucketName, out var publicUrl))
            {
                var sanitizedFolder = folder.Trim('/');
                var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
                var key = $"{sanitizedFolder}/{uniqueFileName}";

                var putRequest = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    InputStream = fileStream,
                    ContentType = contentType,
                    DisablePayloadSigning = true,
                };

                await client.PutObjectAsync(putRequest);

                return $"{publicUrl.TrimEnd('/')}/{sanitizedFolder}/{uniqueFileName}";
            }
        }

        public async Task DeleteFileAsync(string fileUrl)
        {
            // Not an address in the bucket (an upload path from before R2): nothing to remove, and
            // no reason to need the bucket's settings.
            if (GetObjectKey(null, fileUrl) == null)
            {
                return;
            }

            using (AmazonS3Client client = this.CreateClient(out var bucketName, out var publicUrl))
            {
                // Removing an object that is already gone succeeds, so a delete that failed
                // part-way can simply be run again.
                await client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = GetObjectKey(publicUrl, fileUrl),
                });
            }
        }

        private AmazonS3Client CreateClient(out string bucketName, out string publicUrl)
        {
            var accessKey = this.configuration["CloudflareR2:AccessKeyId"];
            var secretKey = this.configuration["CloudflareR2:SecretAccessKey"];
            var serviceUrl = this.configuration["CloudflareR2:ServiceUrl"];
            bucketName = this.configuration["CloudflareR2:BucketName"];
            publicUrl = this.configuration["CloudflareR2:PublicUrl"];

            if (string.IsNullOrWhiteSpace(accessKey) ||
                string.IsNullOrWhiteSpace(secretKey) ||
                string.IsNullOrWhiteSpace(serviceUrl) ||
                string.IsNullOrWhiteSpace(bucketName) ||
                string.IsNullOrWhiteSpace(publicUrl))
            {
                throw new InvalidOperationException("Cloudflare R2 is not fully configured. Missing one or more required settings in CloudflareR2 section.");
            }

            var config = new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = false,
            };

            return new AmazonS3Client(accessKey, secretKey, config);
        }
    }
}
