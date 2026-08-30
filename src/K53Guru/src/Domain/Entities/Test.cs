using K53Guru.Domain.Common.Entities;
using K53Guru.Domain.Enums;

namespace K53Guru.Domain.Entities;

public class Test : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public LicenceCode Codes { get; set; }
    public TestSectionScope Sections { get; set; }
    public TestStatus Status { get; set; } = TestStatus.Draft;

    public List<TestQuestion> TestQuestions { get; set; } = new();
}
