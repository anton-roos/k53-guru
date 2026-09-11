using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Tests.DTOs;

[Description("Tests")]
public class TestDto
{
    [Description("Id")] public int Id { get; set; }

    [Description("Name")] public string? Name { get; set; }

    [Description("Codes")] public LicenceCode Codes { get; set; }

    [Description("Sections")] public TestSectionScope Sections { get; set; }

    [Description("Status")] public TestStatus Status { get; set; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Test, TestDto>(MemberList.None);
        }
    }
}
