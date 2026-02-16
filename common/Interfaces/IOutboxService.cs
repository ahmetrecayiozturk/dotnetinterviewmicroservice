using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace common.Interfaces;
public interface IOutboxService
{
    Task AddEventAsync<TEvent>(TEvent eventData, Guid correlationId, CancellationToken cancellationToken = default)
        where TEvent : class;
}
