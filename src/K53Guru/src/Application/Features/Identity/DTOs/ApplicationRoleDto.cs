// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using K53Guru.Application.Features.Tenants.DTOs;
using K53Guru.Domain.Identity;

namespace K53Guru.Application.Features.Identity.DTOs;

[Description("Roles")]
public class ApplicationRoleDto
{
    [Description("Id")] public string Id { get; set; } = Guid.NewGuid().ToString();
    [Description("Name")] public string Name { get; set; } = string.Empty;
    [Description("Normalized Name")] public string? NormalizedName { get; set; }
    [Description("Description")] public string? Description { get; set; }
    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<ApplicationRole, ApplicationRoleDto>(MemberList.None);
                
        }
    }
}
