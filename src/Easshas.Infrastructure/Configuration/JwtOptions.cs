namespace Easshas.Infrastructure.Configuration
{
    public class JwtOptions
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int ExpiryMinutes { get; set; } = 60;
        public int? AdminExpiryMinutes { get; set; }
        public string CookieName { get; set; } = "access_token";
        public int RefreshExpiryMinutes { get; set; } = 10080; // 7 days
        public string RefreshCookieName { get; set; } = "refresh_token";
    }
}
