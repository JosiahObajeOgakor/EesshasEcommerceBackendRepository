using System;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;

namespace Easshas.Infrastructure.Secrets
{
    public class SecretsProvider
    {
        private readonly IAmazonSecretsManager _secrets;
        private readonly IConfiguration _configuration;

        public SecretsProvider(IConfiguration configuration)
        {
            _configuration = configuration;
            var region = configuration["AWS:Region"] ?? RegionEndpoint.USEast1.SystemName;
            _secrets = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
        }

        public async Task<T?> GetSecretAsync<T>(string secretName) where T : class
        {
            try
            {
                var result = await _secrets.GetSecretValueAsync(new GetSecretValueRequest
                {
                    SecretId = secretName
                });
                if (!string.IsNullOrWhiteSpace(result.SecretString))
                {
                    return JsonSerializer.Deserialize<T>(result.SecretString);
                }
            }
            catch
            {
                // ignore and return null
            }
            return null;
        }
    }
}
