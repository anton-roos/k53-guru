using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Questions.DTOs;

[Description("Questions")]
public class QuestionDto
{
    [Description("Id")] public int Id { get; set; }

    [Description("Stem")] public string? Stem { get; set; }

    [Description("Codes")] public LicenceCode Codes { get; set; }

    [Description("Section")] public SectionType Section { get; set; }

    [Description("Language")] public string? LanguageCode { get; set; }

    [Description("Sign Reference")] public string? SignRef { get; set; }

    /// <summary>
    /// Round-tripped so editing an existing question (QuestionDto -&gt; AddEditQuestionCommand,
    /// see Questions.razor.OnEditQuestion) doesn't silently null out an already-authored
    /// Explanation on save (Story 3.6).
    /// </summary>
    [Description("Explanation")] public string? Explanation { get; set; }

    [Description("Answer Options")] public List<AnswerOptionDto> AnswerOptions { get; set; } = new();

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Question, QuestionDto>(MemberList.None);
            CreateMap<AnswerOption, AnswerOptionDto>(MemberList.None);
        }
    }
}

public class AnswerOptionDto
{
    public int Id { get; set; }
    public string? Text { get; set; }
    public bool IsCorrect { get; set; }
    public int Order { get; set; }
}
