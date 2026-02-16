using common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using common.Models;
using System.Text.Json;

namespace common.Services
{
    //tek tek her service özel dbcontext oluşturmamak için direkt tdbcontext kullanıyoruz
    public class OutboxService<TDbContext> : IOutboxService where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;

        public OutboxService(TDbContext dbContext) 
        {
            this._dbContext = dbContext;
        }

        public async Task AddEventAsync<TEvent>(TEvent eventData, Guid correlationId, CancellationToken cancellationToken = default) where TEvent : class
        {
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = typeof(TEvent).Name,
                Payload = JsonSerializer.Serialize(eventData),
                CorrelationId = correlationId,
                IsProcessed = false,
                CreatedAt = DateTime.UtcNow,
            };

            //var outboxDbSet = _dbContext.Set<OutboxMessage>();
            await _dbContext.Set<OutboxMessage>().AddAsync(outboxMessage, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

        }
    }
}
