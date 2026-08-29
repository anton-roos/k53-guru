using K53Guru.Domain.Identity;

namespace K53Guru.Domain.Events;

    public class LoginAuditCreatedEvent : DomainEvent
    {
        public LoginAuditCreatedEvent(LoginAudit item)
        {
            Item = item;
        }

        public LoginAudit Item { get; }
    }

