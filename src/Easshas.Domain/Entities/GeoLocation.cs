using System;

namespace Easshas.Domain.Entities
{
    public class GeoLocation : BaseEntity
    {
        public Guid OrderId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
