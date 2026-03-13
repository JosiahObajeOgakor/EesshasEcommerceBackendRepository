using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading.Tasks;

namespace Easshas.Infrastructure.Services
{
    public class S3Service
    {
        private readonly string _bucket;
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly string _region;

        public S3Service(IConfiguration config)
        {
            _bucket = config["AWS:Bucket"] ?? "";
            _accessKey = config["AWS:AccessKey"] ?? "";
            _secretKey = config["AWS:SecretKey"] ?? "";
            _region = config["AWS:Region"] ?? "us-east-1";
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
        {
            var s3Client = new AmazonS3Client(_accessKey, _secretKey, Amazon.RegionEndpoint.GetBySystemName(_region));
            var transferUtility = new TransferUtility(s3Client);
            var request = new TransferUtilityUploadRequest
            {
                InputStream = fileStream,
                Key = fileName,
                BucketName = _bucket,
                ContentType = contentType
            };
            await transferUtility.UploadAsync(request);

            return $"https://{_bucket}.s3.{_region}.amazonaws.com/{fileName}";
        }
    }
}
