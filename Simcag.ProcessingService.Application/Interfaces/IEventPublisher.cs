using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Simcag.Shared.Events;

namespace Simcag.ProcessingService.Application.Interfaces
{
    public interface IEventPublisher
    {
        Task PublishAsync<T>(T eventMessage, CancellationToken cancellationToken = default) where T : BaseEvent;
    }
}