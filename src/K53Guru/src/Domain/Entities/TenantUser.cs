using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using K53Guru.Domain.Common.Entities;
using K53Guru.Domain.Identity;

namespace K53Guru.Domain.Entities;

public class TenantUser:IEntity<string>
{
    public string Id { get; set; }=Guid.CreateVersion7().ToString();
    public string? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
}
