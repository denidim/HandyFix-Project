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

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
        {
            var accessKey = this.configuration["CloudflareR2:AccessKeyId"];
            var secretKey = this.configuration["CloudflareR2:SecretAccessKey"];
            var serviceUrl = this.configuration["CloudflareR2:ServiceUrl"];
            var bucketName = this.configuration["CloudflareR2:BucketName"];
            var publicUrl = this.configuration["CloudflareR2:PublicUrl"];

            var config = new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = false,
            };

            using (var client = new AmazonS3Client(accessKey, secretKey, config))
            {
                var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
                var key = $"inquiries/{uniqueFileName}";

                var putRequest = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    InputStream = fileStream,
                    ContentType = contentType,
                    DisablePayloadSigning = true,
                };

                await client.PutObjectAsync(putRequest);

                return $"{publicUrl.TrimEnd('/')}/inquiries/{uniqueFileName}";
            }
        }
    }
}
