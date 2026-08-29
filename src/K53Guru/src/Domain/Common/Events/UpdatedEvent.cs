using K53Guru.Domain.Common.Entities;

namespace K53Guru.Domain.Common.Events;

public class UpdatedEvent<T> : DomainEvent where T : IEntity
{
    public UpdatedEvent(T entity)
    {
        Entity = entity;
    }

    public T Entity { get; }
}
