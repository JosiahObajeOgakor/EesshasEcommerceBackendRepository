using System.Threading.Tasks;
using Easshas.Domain.Entities;

namespace Easshas.Application.Abstractions
{
    public interface IEmailSender
    {
        Task SendOrderConfirmationAsync(Order order, string userEmail, string adminEmail, string? userName = null);
        Task SendOrderCreatedAsync(Order order, string userEmail, string adminEmail, string trackingUrl, string? userName = null);
        Task SendLowStockAlertAsync(Product product, string adminEmail);
        Task SendLowStockWarningToUserAsync(Product product, string userEmail);
        Task SendOrderStatusChangedAsync(Order order, string userEmail, string status, string? note = null, string? userName = null);
        Task SendAdminNewOrderAlertAsync(Order order, string adminEmail);
        Task SendGenericEmailAsync(string to, string subject, string htmlContent, string? bcc = null);
    }
}
