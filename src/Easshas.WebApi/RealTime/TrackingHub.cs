using Microsoft.AspNetCore.SignalR;

namespace Easshas.WebApi.RealTime
{
    // Proxy class to expose TrackingHub in WebApi for DI and controller references
    public class TrackingHub : Easshas.Infrastructure.RealTime.TrackingHub { }
}
