namespace AppTemplate.SharedKernel;

public interface IDomainEventHandler<T> : INotificationHandler<T>
    where T : IDomainEvent { }
