using System.Threading.Tasks;
using Easshas.Domain.Entities;

namespace Easshas.Application.Abstractions
{
    public interface IWhatsAppNotifier
    {
        Task NotifyOrderConfirmedAsync(Order order, string userPhone, string adminPhone);
        Task SendTextAsync(string toPhone, string text);
    }
}
