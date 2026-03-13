using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Easshas.Infrastructure.RealTime
{
    public class TrackingHub : Hub
    {
        public async Task JoinAdminsGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        }

        public async Task LeaveAdminsGroup()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");
        }

        public async Task JoinOrderGroup(string orderId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"order:{orderId}");
        }

        public async Task LeaveOrderGroup(string orderId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order:{orderId}");
        }
    }
}
