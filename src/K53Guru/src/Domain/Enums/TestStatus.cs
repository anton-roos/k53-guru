namespace K53Guru.Domain.Enums;

/// <summary>
/// The publication state of a <see cref="Entities.Test"/>. Defaults to Draft on create; only
/// Story 2.3's publish/unpublish command ever transitions it to Published (and back).
/// </summary>
public enum TestStatus
{
    Draft,
    Published
}
