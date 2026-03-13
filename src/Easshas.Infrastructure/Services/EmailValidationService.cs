using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using Easshas.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Easshas.Infrastructure.Services
{
    public class EmailValidationService : IEmailValidationService
    {
        private readonly string[] _recognized;
        private readonly string[] _disposable;

        public EmailValidationService(IConfiguration configuration)
        {
            _recognized = (configuration.GetSection("Email:RecognizedProviders").Get<string[]>() ?? Array.Empty<string>())
                .Select(d => d.Trim().ToLowerInvariant())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .ToArray();

            _disposable = (configuration.GetSection("Email:DisposableDomains").Get<string[]>() ?? Array.Empty<string>())
                .Select(d => d.Trim().ToLowerInvariant())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .ToArray();
        }

        public bool IsValidFormat(string email)
        {
            try
            {
                var addr = new MailAddress(email);
                return addr.Address.Equals(email, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string ExtractDomain(string email)
        {
            var at = email.LastIndexOf('@');
            return at >= 0 ? email[(at + 1)..].Trim().ToLowerInvariant() : string.Empty;
        }

        public bool IsRecognizedDomain(string email)
        {
            var domain = ExtractDomain(email);
            if (string.IsNullOrWhiteSpace(domain)) return false;
            if (_recognized.Length == 0)
            {
                // Default built-ins if none configured
                var builtins = new[] { "gmail.com", "yahoo.com", "outlook.com", "hotmail.com", "live.com", "zoho.com", "protonmail.com", "icloud.com", "yandex.com" };
                return builtins.Contains(domain);
            }
            return _recognized.Contains(domain);
        }

        public bool IsDisposableDomain(string email)
        {
            var domain = ExtractDomain(email);
            if (string.IsNullOrWhiteSpace(domain)) return true;
            return _disposable.Contains(domain);
        }

        public bool IsAcceptable(string email)
        {
            return IsValidFormat(email) && IsRecognizedDomain(email) && !IsDisposableDomain(email);
        }

        public IReadOnlyList<string> GetRecognizedDomains() => _recognized.Length == 0
            ? new[] { "gmail.com", "yahoo.com", "outlook.com", "hotmail.com", "live.com", "zoho.com", "protonmail.com", "icloud.com", "yandex.com" }
            : _recognized;
    }
}
