// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using K53Guru.Domain.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace K53Guru.Application.Common.Interfaces;

public interface IApplicationDbContext: IAsyncDisposable
{
    DbSet<SystemLog> SystemLogs { get; set; }
    DbSet<AuditTrail> AuditTrails { get; set; }
    DbSet<Document> Documents { get; set; }
    DbSet<PicklistSet> PicklistSets { get; set; }
    DbSet<Product> Products { get; set; }
    DbSet<Tenant> Tenants { get; set; }
    DbSet<TenantUser> TenantUsers { get; set; }
    DbSet<Contact> Contacts { get; set; }
    DbSet<RoadSign> RoadSigns { get; set; }
    DbSet<Question> Questions { get; set; }
    DbSet<AnswerOption> AnswerOptions { get; set; }
    DbSet<Test> Tests { get; set; }
    DbSet<TestQuestion> TestQuestions { get; set; }
    DbSet<TestConfig> TestConfigs { get; set; }
    DbSet<SectionRule> SectionRules { get; set; }
    DbSet<LearnerProfile> LearnerProfiles { get; set; }
    DbSet<Attempt> Attempts { get; set; }
    DbSet<AttemptQuestion> AttemptQuestions { get; set; }
    DbSet<AttemptAnswerOption> AttemptAnswerOptions { get; set; }
    DbSet<CodeResult> CodeResults { get; set; }
    DbSet<SectionResult> SectionResults { get; set; }
    DbSet<LoginAudit> LoginAudits { get; set; }
    DbSet<UserLoginRiskSummary> UserLoginRiskSummaries { get; set; }
    ChangeTracker ChangeTracker { get; }

    DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
