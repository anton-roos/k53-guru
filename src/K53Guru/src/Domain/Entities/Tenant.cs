using K53Guru.Domain.Common.Entities;

namespace K53Guru.Domain.Entities;

public class Tenant : IEntity<string>
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string Id { get; set; } = Guid.CreateVersion7().ToString();
    public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
}
