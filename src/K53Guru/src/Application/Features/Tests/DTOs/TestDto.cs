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

    [Description("Questions")] public List<TestQuestionSummaryDto> Questions { get; set; } = new();

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Test, TestDto>(MemberList.None)
                .ForMember(d => d.Questions, opt => opt.MapFrom(s => s.TestQuestions.Select(tq => tq.Question)));

            CreateMap<Question, TestQuestionSummaryDto>(MemberList.None);
        }
    }
}

/// <summary>
/// Flat per-question summary of a Test's associated questions. Grouping/counting by section and
/// code for display is plain LINQ over this list in the Razor code-behind - never a server-side
/// pre-computed count DTO.
/// </summary>
public class TestQuestionSummaryDto
{
    public int Id { get; set; }
    public string? Stem { get; set; }
    public LicenceCode Codes { get; set; }
    public SectionType Section { get; set; }
}
