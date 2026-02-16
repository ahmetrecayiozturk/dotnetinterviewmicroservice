using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace common.Models
{
    public class OutboxMessage
    {
        public Guid Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public Guid CorrelationId { get; set; }
        //bunu kullanarak idempotency sağlıyoruz yani bir event iki kere işlenemiyor
        public bool IsProcessed { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public int RetryCount { get; set; } = 0;
        public string? LastError { get; set; }
    }

}
