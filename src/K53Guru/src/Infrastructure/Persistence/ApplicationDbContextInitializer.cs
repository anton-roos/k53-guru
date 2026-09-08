using System;
using System.Reflection;
using System.Text.Json;
using K53Guru.Application.Common.Constants;
using K53Guru.Application.Common.Security;
using K53Guru.Domain.Enums;
using K53Guru.Domain.Identity;

namespace K53Guru.Infrastructure.Persistence;

public class ApplicationDbContextInitializer
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ApplicationDbContextInitializer> _logger;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicationDbContextInitializer(ILogger<ApplicationDbContextInitializer> logger,
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _logger = logger;
        _context = dbContextFactory.CreateDbContext();
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            if (_context.Database.IsRelational())
                await _context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initialising the database");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await SeedTenantsAsync();
            await SeedRolesAsync();
            await SeedUsersAsync();
            await SeedDataAsync();
            _context.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }

    private static IEnumerable<string> GetAllPermissions()
    {
        var allPermissions = new List<string>();
        var modules = typeof(Permissions).GetNestedTypes();

        foreach (var module in modules)
        {
            var moduleName = string.Empty;
            var moduleDescription = string.Empty;

            var fields = module.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            foreach (var fi in fields)
            {
                var propertyValue = fi.GetValue(null);

                if (propertyValue is not null)
                    allPermissions.Add((string)propertyValue);
            }
        }

        return allPermissions;
    }




    private async Task SeedTenantsAsync()
    {
        if (await _context.Tenants.AnyAsync()) return;

        _logger.LogInformation("Seeding organizations...");
        var tenants = new[]
        {
                new Tenant { Name = "Main", Description = "Main Site" },
                new Tenant { Name = "Europe", Description = "Europe Site" }
            };

        await _context.Tenants.AddRangeAsync(tenants);
        await _context.SaveChangesAsync();
    }

    private async Task SeedRolesAsync()
    {
        var adminRoleName = Roles.Admin;
        var userRoleName = Roles.Basic;

        if (await _roleManager.RoleExistsAsync(adminRoleName)) return;

        _logger.LogInformation("Seeding roles...");
        var administratorRole = new ApplicationRole(adminRoleName)
        {
            Description = "Admin Group",
            CreatedAt= DateTime.UtcNow,
        };
        var userRole = new ApplicationRole(userRoleName)
        {
            Description = "Basic Group",
            CreatedAt = DateTime.UtcNow,
        };

        await _roleManager.CreateAsync(administratorRole);
        await _roleManager.CreateAsync(userRole);

        var permissions = GetAllPermissions();

        foreach (var permission in permissions)
        {
            var claim = new Claim(ApplicationClaimTypes.Permission, permission);
            await _roleManager.AddClaimAsync(administratorRole, claim);

            if (permission.StartsWith("Permissions.Products"))
            {
                await _roleManager.AddClaimAsync(userRole, claim);
            }
        }
    }

    private async Task SeedUsersAsync()
    {
        if (await _userManager.Users.AnyAsync()) return;

        _logger.LogInformation("Seeding users...");
        var tenants = await _context.Tenants.ToListAsync();
        var adminUser = new ApplicationUser
        {
            UserName = Users.Administrator,
            Provider = "Local",
            IsActive = true,
            TenantId = (await _context.Tenants.FirstAsync()).Id,
            DisplayName = Users.Administrator,
            Email = "admin@example.com",
            EmailConfirmed = true,
            ProfilePictureDataUrl = "https://s.gravatar.com/avatar/78be68221020124c23c665ac54e07074?s=80",
            LanguageCode="en-US",
            TimeZoneId= "Asia/Shanghai",
            TwoFactorEnabled = false,
            CreatedAt=DateTime.UtcNow,
            TenantUsers = tenants.Select(t => new TenantUser { TenantId = t.Id }).ToList()
        };
        await _userManager.CreateAsync(adminUser, Users.DefaultPassword);
        await _userManager.AddToRoleAsync(adminUser, Roles.Admin);
        var demoUser = new ApplicationUser
        {
            UserName = Users.Demo,
            IsActive = true,
            Provider = "Local",
            TenantId = (await _context.Tenants.FirstAsync()).Id,
            DisplayName = Users.Demo,
            SuperiorId = adminUser.Id,
            Email = "demo@example.com",
            EmailConfirmed = true,
            LanguageCode = "de-DE",
            TimeZoneId = "Europe/Berlin",
            TenantUsers = new List<TenantUser> { new TenantUser { TenantId = tenants.First().Id } },
            ProfilePictureDataUrl = "https://s.gravatar.com/avatar/ea753b0b0f357a41491408307ade445e?s=80",
            CreatedAt = DateTime.UtcNow
        };

       

        await _userManager.CreateAsync(demoUser, Users.DefaultPassword);
        await _userManager.AddToRoleAsync(demoUser, Roles.Basic);
    }

    private async Task SeedDataAsync()
    {
        // PicklistSet/Product seeding (starter template demo data) removed alongside the
        // Picklist/Products admin pages and their Application-layer features -- nothing left
        // reads or manages this data. See chat: admin panel cleanup.

        await SeedRoadSignsAsync();
        await SeedQuestionsAsync();
        await SeedTestConfigsAsync();
    }

    /// <summary>
    /// Codes seeded in an earlier iteration that turned out not to be real SADC/K53
    /// legislation codes at all (either invented placeholders, e.g. "W1"-"W5"/"GS1"/"GS2",
    /// or a code written in a non-canonical shortened form, e.g. bare "R201" instead of the
    /// real numbered variant "R201-60") -- confirmed against
    /// https://simple.wikipedia.org/wiki/Road_signs_in_South_Africa. Removed on startup if
    /// still present so a database seeded before this correction gets cleaned up too.
    /// </summary>
    private static readonly string[] ObsoleteRoadSignCodes = { "R4", "R201", "W1", "W2", "W3", "W4", "W5", "GS1", "GS2" };

    private sealed record RoadSignSeedEntry(string LegislationCode, string Description, string ImageAssetKey, string Confidence);

    /// <summary>
    /// Seeds/updates the road-sign catalog from the embedded <c>road-signs-catalog.json</c>
    /// resource (406 entries covering the full SADC/K53 R/W/TW/IN numbering, cross-referenced
    /// against https://simple.wikipedia.org/wiki/Road_signs_in_South_Africa -- see that file's
    /// own "confidence" field: "confirmed" is sourced verbatim from that page; "inferred-*"
    /// entries follow a confirmed real pattern (e.g. the "-600" suffix's "end of restriction"
    /// meaning) but aren't independently confirmed per-code; "unverified"/"no-image-yet" are
    /// honest placeholders pending a real source -- see deferred-work.md).
    ///
    /// Runs every startup (not gated on "table is empty") so a) a database seeded before this
    /// correction gets its inaccurate legacy entries (see <see cref="ObsoleteRoadSignCodes"/>)
    /// removed and its remaining entries corrected, and b) future catalog growth (editing the
    /// embedded JSON) reaches an already-running database without a manual data migration.
    /// </summary>
    private async Task SeedRoadSignsAsync()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "K53Guru.Infrastructure.Persistence.SeedData.road-signs-catalog.json";
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var entries = await JsonSerializer.DeserializeAsync<List<RoadSignSeedEntry>>(stream, jsonOptions)
            ?? new List<RoadSignSeedEntry>();

        var obsolete = await _context.RoadSigns
            .Where(r => ObsoleteRoadSignCodes.Contains(r.LegislationCode))
            .ToListAsync();
        if (obsolete.Count > 0)
        {
            _logger.LogInformation("Removing {Count} obsolete/invalid road sign catalog entries...", obsolete.Count);
            _context.RoadSigns.RemoveRange(obsolete);
        }

        var existing = await _context.RoadSigns.ToDictionaryAsync(r => r.LegislationCode, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var updated = 0;
        foreach (var entry in entries)
        {
            if (existing.TryGetValue(entry.LegislationCode, out var sign))
            {
                if (sign.Description != entry.Description || sign.ImageAssetKey != entry.ImageAssetKey)
                {
                    sign.Description = entry.Description;
                    sign.ImageAssetKey = entry.ImageAssetKey;
                    updated++;
                }
            }
            else
            {
                _context.RoadSigns.Add(new RoadSign
                {
                    LegislationCode = entry.LegislationCode,
                    Description = entry.Description,
                    ImageAssetKey = entry.ImageAssetKey
                });
                added++;
            }
        }

        if (obsolete.Count > 0 || added > 0 || updated > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "Road sign catalog: {Added} added, {Updated} updated, {Removed} obsolete removed.",
                added, updated, obsolete.Count);
        }
    }

    /// <summary>
    /// Seeds a starter batch of K53-style multiple-choice <see cref="Question"/>s (Rules,
    /// Signs, VehicleControls) so the catalog isn't empty for browsing/testing. NOT an
    /// exhaustive question bank -- real content authoring is the Admin Panel's job (Epic 2).
    /// Signs-section questions reference a real <see cref="RoadSign.LegislationCode"/> via
    /// <see cref="Question.SignRef"/> and use that sign's own verified
    /// (see <see cref="RoadSignSeedEntry"/>/road-signs-catalog.json) description as the
    /// correct answer, with distractors drawn from OTHER real sign descriptions in the
    /// catalog rather than invented text. Rules/VehicleControls content is limited to
    /// well-established, low-risk-of-error general road-rule/vehicle-operation knowledge.
    /// Tagged for all three licence codes (Code1|Code2|Code3) since none of this content is
    /// code-specific. Not yet linked to any <see cref="Test"/> via <see cref="TestQuestion"/>
    /// -- that composition step is separate (Epic 2 admin authoring).
    /// </summary>
    /// <summary>
    /// Stem of the first question in this seed batch, used as a sentinel to detect whether
    /// this specific batch has already been seeded -- deliberately NOT a blanket
    /// "any question exists" check, since a real admin (via the Admin Panel's Questions page)
    /// may have already authored unrelated questions before this batch ever runs, and those
    /// must not block or be duplicated by it.
    /// </summary>
    private const string SeedQuestionsSentinelStem =
        "At a four-way stop where two vehicles arrive at the same time, which vehicle has right of way?";

    private async Task SeedQuestionsAsync()
    {
        var alreadySeeded = await _context.Questions.AnyAsync(q => q.Stem == SeedQuestionsSentinelStem);
        if (alreadySeeded) return;

        _logger.LogInformation("Seeding starter questions...");
        const LicenceCode allCodes = LicenceCode.Code1 | LicenceCode.Code2 | LicenceCode.Code3;

        Question Q(string stem, SectionType section, string? signRef, string? explanation,
            params (string Text, bool IsCorrect)[] options) => new()
        {
            Stem = stem,
            Codes = allCodes,
            Section = section,
            SignRef = signRef,
            Explanation = explanation,
            AnswerOptions = options.Select((o, i) => new AnswerOption
            {
                Text = o.Text,
                IsCorrect = o.IsCorrect,
                Order = i
            }).ToList()
        };

        var questions = new List<Question>
        {
            // Rules
            Q("At a four-way stop where two vehicles arrive at the same time, which vehicle has right of way?",
                SectionType.Rules, null,
                "The vehicle approaching from the right has right of way at a simultaneous four-way stop.",
                ("The vehicle approaching from the right", true),
                ("The heavier vehicle", false),
                ("Whichever vehicle hoots first", false),
                ("Right of way is decided by vehicle colour", false)),
            Q("What is the recommended following distance between your vehicle and the one in front under normal conditions?",
                SectionType.Rules, null,
                "A following gap of at least 3 seconds gives you time to react if the vehicle ahead brakes suddenly.",
                ("At least a 3-second gap", true),
                ("As close as possible to save space", false),
                ("Exactly one car length, regardless of speed", false),
                ("There is no recommended following distance", false)),
            Q("Who is legally required to wear a seatbelt while a vehicle is in motion?",
                SectionType.Rules, null,
                "Seatbelt use is compulsory for the driver and every passenger, front or back.",
                ("The driver and all passengers, including those in the back seat", true),
                ("Only the driver", false),
                ("Only front-seat passengers", false),
                ("Seatbelts are optional for adults", false)),
            Q("When approaching a Give Way / Yield sign, what must a driver do?",
                SectionType.Rules, "R2",
                "A Yield sign means slow down and be ready to stop for traffic on the road you are joining.",
                ("Slow down and, if necessary, stop to give way to traffic on the intersecting road", true),
                ("Always come to a complete stop regardless of traffic", false),
                ("Speed up to clear the intersection quickly", false),
                ("Ignore the sign if no other vehicles are visible", false)),
            Q("When may a driver legally overtake across a solid barrier line?",
                SectionType.Rules, null,
                "A solid barrier line means overtaking is prohibited, regardless of how clear the road looks.",
                ("Never — overtaking across a solid barrier line is prohibited", true),
                ("Only at night", false),
                ("Only if the vehicle ahead is travelling very slowly", false),
                ("Only on a freeway", false)),
            Q("At a marked pedestrian crossing, when must a driver give way to a pedestrian?",
                SectionType.Rules, null,
                "Once a pedestrian has stepped onto a marked crossing, approaching traffic must give way.",
                ("When the pedestrian is already on the crossing", true),
                ("Only if the pedestrian is a child", false),
                ("Only during school hours", false),
                ("A driver never has to give way to pedestrians", false)),
            Q("Before changing lanes or turning, what must a driver do?",
                SectionType.Rules, null,
                "Checking mirrors and signalling in good time lets other road users react safely.",
                ("Check mirrors and give the appropriate indicator signal in good time", true),
                ("Change lanes first, then signal", false),
                ("No signal is required if the road is empty", false),
                ("Sound the hooter instead of indicating", false)),
            Q("What does a Stop sign require a driver to do?",
                SectionType.Rules, "R1",
                "A Stop sign always requires a complete standstill, even if the road looks clear.",
                ("Come to a complete standstill before the stop line, then proceed only when safe", true),
                ("Slow down but continue moving if no traffic is visible", false),
                ("Stop only if another vehicle is present", false),
                ("Stop applies only during the day", false)),
            Q("When should a driver use hazard warning lights?",
                SectionType.Rules, null,
                "Hazard lights warn other road users of a danger ahead or that your vehicle itself is a hazard.",
                ("To warn other road users of a hazard, or when the vehicle is stopped in a dangerous position", true),
                ("Whenever driving in the rain", false),
                ("Whenever reversing", false),
                ("As a substitute for indicators when turning", false)),
            Q("In the absence of any posted speed limit sign, what is the general speed limit on a South African freeway?",
                SectionType.Rules, null,
                "South Africa's default (unsigned) limits are 60 km/h in urban areas, 100 km/h on other public roads, and 120 km/h on freeways.",
                ("120 km/h", true),
                ("100 km/h", false),
                ("140 km/h", false),
                ("80 km/h", false)),

            // Signs (each references a real, verified RoadSign via SignRef)
            Q("What does this road sign mean?", SectionType.Signs, "R1", null,
                ("Stop", true), ("Give Way / Yield", false), ("No entry", false), ("Pedestrian priority zone", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R2", null,
                ("Give Way / Yield", true), ("Stop", false), ("No entry", false), ("Keep Left", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R3", null,
                ("No entry", true), ("Stop", false), ("Give Way / Yield", false), ("Minimum speed limit", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R5", null,
                ("Pedestrian priority zone", true), ("No entry", false), ("Roundabout", false), ("Toll road", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R101", null,
                ("Minimum speed limit", true), ("Right turn prohibited ahead", false),
                ("Keep Left", false), ("Vehicles exceeding 10 tonnes GVM only", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R103", null,
                ("Keep Left", true), ("Keep Right", false), ("Turn Left", false), ("Proceed Straight", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R213", null,
                ("U-turn prohibited", true), ("Overtaking prohibited", false),
                ("Parking prohibited", false), ("Left turn prohibited", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R216", null,
                ("Parking prohibited", true), ("Stopping prohibited", false),
                ("Overtaking prohibited", false), ("Cyclists prohibited", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R217", null,
                ("Stopping prohibited", true), ("Parking prohibited", false),
                ("U-turn prohibited", false), ("Pedestrians prohibited", false)),
            Q("What does this road sign mean?", SectionType.Signs, "W101", null,
                ("Crossroad ahead", true), ("T-junction ahead", false),
                ("Roundabout ahead", false), ("Fork ahead", false)),
            Q("What does this road sign mean?", SectionType.Signs, "W201", null,
                ("Roundabout ahead", true), ("Sharp curve ahead", false),
                ("Crossroad ahead", false), ("Winding road ahead", false)),
            Q("What does this road sign mean?", SectionType.Signs, "W204", null,
                ("Sharp curve ahead", true), ("Gentle curve ahead", false),
                ("Hairpin curve ahead", false), ("Series of curves ahead", false)),
            Q("What does this road sign mean?", SectionType.Signs, "W306", null,
                ("Pedestrian crossing ahead", true), ("Children ahead", false),
                ("Cyclists ahead", false), ("Pedestrians ahead", false)),
            Q("What does this road sign mean?", SectionType.Signs, "W308", null,
                ("Children ahead", true), ("Pedestrians ahead", false),
                ("Cattle ahead", false), ("Cyclists ahead", false)),
            Q("What does this road sign mean?", SectionType.Signs, "W333", null,
                ("Slippery road ahead", true), ("Uneven road surface ahead", false),
                ("Falling rocks ahead", false), ("Unpaved road surface ahead", false)),

            // VehicleControls
            Q("What should a driver do with the mirrors before moving off?",
                SectionType.VehicleControls, null,
                "Correctly adjusted mirrors are essential for seeing traffic behind and beside you.",
                ("Adjust all mirrors to give a clear view of the road behind and to the sides", true),
                ("Mirrors only need adjusting once a year", false),
                ("Fold both side mirrors in while driving", false),
                ("Mirrors are only necessary when reversing", false)),
            Q("When should the handbrake (parking brake) be applied?",
                SectionType.VehicleControls, null,
                "The handbrake should be applied every time the vehicle is parked, and is essential on an incline.",
                ("Whenever the vehicle is parked, especially on an incline", true),
                ("Only when parked on a flat road", false),
                ("Only at night", false),
                ("The handbrake should never be used", false)),
            Q("What is the correct way to change gears smoothly in a manual vehicle?",
                SectionType.VehicleControls, null,
                "Fully depressing the clutch before shifting protects the gearbox and gives a smooth change.",
                ("Depress the clutch fully before selecting the next gear, then release it smoothly", true),
                ("Change gears without using the clutch", false),
                ("Keep the clutch partially depressed at all times while driving", false),
                ("Release the clutch as fast as possible after every gear change", false)),
            Q("Before starting the engine, which checks should a driver perform?",
                SectionType.VehicleControls, null,
                "Seat, mirrors and seatbelt should always be set correctly before you start driving.",
                ("Adjust the seat and mirrors, and fasten the seatbelt", true),
                ("Check only the fuel gauge", false),
                ("No checks are required before starting", false),
                ("Check that the boot is locked", false)),
            Q("Why is it important for tyres to have sufficient tread depth?",
                SectionType.VehicleControls, null,
                "Tread depth is what lets a tyre grip the road and channel away water in wet conditions.",
                ("To maintain proper grip on the road, especially in wet conditions", true),
                ("To make the vehicle quieter", false),
                ("To improve fuel economy only", false),
                ("Tread depth has no effect on safety", false)),
            Q("When must a driver activate the indicator (turn signal)?",
                SectionType.VehicleControls, null,
                "Signalling early gives other road users time to react to your intended turn or lane change.",
                ("In good time before turning or changing lanes, to warn other road users", true),
                ("Only after starting the turn", false),
                ("Only when other vehicles are visible nearby", false),
                ("Indicators are optional on quiet roads", false)),
        };

        await _context.Questions.AddRangeAsync(questions);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} starter questions.", questions.Count);
    }

    /// <summary>
    /// Seeds one <see cref="TestConfig"/> per <see cref="LicenceCode"/> (Code1/Code2/Code3),
    /// each with 3 <see cref="SectionRule"/> children (Rules/Signs/VehicleControls).
    ///
    /// Every numeric value below is a PROVISIONAL PLACEHOLDER, not a confirmed official
    /// K53/CLLT figure - see test-structure.md (`_bmad-output/specs/spec-k53-learners-app/test-structure.md`)
    /// and its "Confirm the final ranges and the time limit against a live DLTC/CLLT
    /// terminal before relying on them" caveat, plus the tracking entry in deferred-work.md
    /// (spec-3-2-configure-test-parameters.md). Identical across all three codes today,
    /// since the source document documents no per-code numeric variance.
    /// </summary>
    private async Task SeedTestConfigsAsync()
    {
        if (await _context.TestConfigs.AnyAsync()) return;

        _logger.LogInformation("Seeding test configs...");

        // PROVISIONAL PLACEHOLDER: no time limit is specified anywhere in test-structure.md
        // ("Not specified in the CLLT description provided; unconfirmed... Confirm before
        // enforcing."). 60 minutes is a generous placeholder pending real-world confirmation.
        const int placeholderTimeLimitMinutes = 60;

        var codes = new[] { LicenceCode.Code1, LicenceCode.Code2, LicenceCode.Code3 };

        var testConfigs = codes.Select(code => new TestConfig
        {
            Code = code,
            TimeLimitMinutes = placeholderTimeLimitMinutes,
            SectionRules = new List<SectionRule>
            {
                // PROVISIONAL PLACEHOLDER: test-structure.md documents an official *range*
                // of 28-30 questions with a pass mark of 22 correct. 30 (the upper end of
                // the range) is seeded as a single representative value, per explicit human
                // direction not to model ranges.
                new SectionRule { Section = SectionType.Rules, QuestionCount = 30, PassMark = 22 },

                // PROVISIONAL PLACEHOLDER: test-structure.md documents an official *range*
                // of 28-30 questions with a pass mark of 23 correct. 30 (the upper end of
                // the range) is seeded as a single representative value.
                new SectionRule { Section = SectionType.Signs, QuestionCount = 30, PassMark = 23 },

                // PROVISIONAL PLACEHOLDER: test-structure.md documents an official *range*
                // of 8-12 questions with a pass mark of "6 of 8, or 10 of 12" depending on
                // which count is drawn. 12/10 (the upper end of the range, paired with its
                // documented pass mark) is seeded as a single representative value.
                new SectionRule { Section = SectionType.VehicleControls, QuestionCount = 12, PassMark = 10 }
            }
        }).ToArray();

        await _context.TestConfigs.AddRangeAsync(testConfigs);
        await _context.SaveChangesAsync();
    }
}
