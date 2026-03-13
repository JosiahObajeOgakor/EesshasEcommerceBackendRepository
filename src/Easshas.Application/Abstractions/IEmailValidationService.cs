using System.Collections.Generic;

namespace Easshas.Application.Abstractions
{
    public interface IEmailValidationService
    {
        bool IsValidFormat(string email);
        bool IsRecognizedDomain(string email);
        bool IsDisposableDomain(string email);
        bool IsAcceptable(string email);
        IReadOnlyList<string> GetRecognizedDomains();
    }
}