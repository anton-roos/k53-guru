using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Tests.DTOs;

/// <summary>
/// A published, servable sitting as discoverable via <c>GET /api/v1/sittings</c>. Mirrors
/// <see cref="TestDto"/>'s shape but is intentionally thin - no section/question detail, which
/// belongs to the "start attempt" endpoint (Story 3.3), not discovery.
/// </summary>
[Description("Available Sittings")]
public class AvailableSittingDto
{
    [Description("Id")] public int Id { get; set; }

    [Description("Codes")] public LicenceCode Codes { get; set; }

    [Description("Name")] public string? Name { get; set; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Test, AvailableSittingDto>(MemberList.None);
        }
    }
}
