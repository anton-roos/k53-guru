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
        await SeedMoreQuestionsAsync();
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
    /// code-specific. <see cref="Test"/>s no longer curate an explicit question pool -- attempt
    /// composition (<c>StartAttemptCommand</c>) draws directly from this bank, filtered by
    /// <see cref="Question.Section"/>/<see cref="Question.Codes"/>, so a seeded question is
    /// immediately eligible for every matching sitting.
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
    /// Sentinel for the second question batch (see <see cref="SeedMoreQuestionsAsync"/>) --
    /// same technique as <see cref="SeedQuestionsSentinelStem"/>, a separate sentinel because
    /// this batch was added after the first one had already been seeded on a live database.
    /// </summary>
    private const string SeedMoreQuestionsSentinelStem =
        "At a railway level crossing with flashing lights or a lowered boom, what must a driver do?";

    /// <summary>
    /// Tops up the question bank to the real minimums <see cref="SeedTestConfigsAsync"/>'s
    /// SectionRules require to actually start an attempt (30 Rules, 30 Signs, 12
    /// VehicleControls -- see <c>StartAttemptCommand</c>'s pool-sufficiency check, which
    /// applies to Practice attempts exactly the same as Test attempts). The first batch (see
    /// <see cref="SeedQuestionsAsync"/>) only had 10/15/6 -- nowhere near enough for any
    /// attempt to ever start; this batch adds the remaining 20/15/6 needed to exactly meet
    /// the minimum (no slack for shuffle variety yet -- every attempt draws the same set,
    /// just reordered -- until the bank grows further via real content authoring, Epic 2).
    /// Same sourcing standard as the first batch: Signs questions reference a real, verified
    /// <see cref="RoadSign.LegislationCode"/>; Rules/VehicleControls content is limited to
    /// well-established, low-risk-of-error general road-rule/vehicle-operation knowledge.
    /// </summary>
    private async Task SeedMoreQuestionsAsync()
    {
        var alreadySeeded =
            await _context.Questions.AnyAsync(q => q.Stem == SeedMoreQuestionsSentinelStem);
        if (alreadySeeded) return;

        _logger.LogInformation("Seeding additional starter questions...");
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
            // Rules (20)
            Q("What must a driver do when an emergency vehicle approaches with its siren and lights on?",
                SectionType.Rules, null,
                "Emergency vehicles need a clear path -- pull over safely and let them pass.",
                ("Move out of the way and give way as soon as it's safe to do so", true),
                ("Speed up to clear the intersection first", false),
                ("Ignore it if you have right of way", false),
                ("Stop immediately wherever you are", false)),
            Q("May a driver use a hand-held mobile phone while driving?",
                SectionType.Rules, null,
                "Using a hand-held phone while driving is illegal and a major distraction.",
                ("No, it is illegal", true),
                ("Yes, as long as the call is short", false),
                ("Yes, but only for texting", false),
                ("Yes, if driving below the speed limit", false)),
            Q("Is it legal to drive under the influence of alcohol or drugs?",
                SectionType.Rules, null,
                "Driving under the influence is illegal and one of the leading causes of serious accidents.",
                ("No, it is illegal", true),
                ("Yes, if driving carefully", false),
                ("Yes, on quiet roads only", false),
                ("Only during the day", false)),
            Q("At a railway level crossing with flashing lights or a lowered boom, what must a driver do?",
                SectionType.Rules, null,
                "Flashing lights or a lowered boom mean a train is approaching -- always stop and wait.",
                ("Stop and wait until it is safe to proceed", true),
                ("Proceed quickly before the train arrives", false),
                ("Sound the hooter and continue", false),
                ("Reverse away from the crossing", false)),
            Q("At a roundabout, who must give way?",
                SectionType.Rules, null,
                "Traffic entering a roundabout must give way to vehicles already circulating in it.",
                ("Traffic entering the roundabout gives way to traffic already circulating", true),
                ("Traffic already circulating always gives way to entering traffic", false),
                ("Whoever arrives first, regardless of position", false),
                ("The largest vehicle has right of way", false)),
            Q("How should a driver behave in a marked school zone?",
                SectionType.Rules, null,
                "School zones need reduced speed and extra caution for children near the road.",
                ("Reduce speed and take extra care", true),
                ("Maintain normal speed", false),
                ("Speed up to clear the zone quickly", false),
                ("School zones only apply on weekends", false)),
            Q("Should a driver follow closely behind an emergency vehicle using its siren?",
                SectionType.Rules, null,
                "Following an emergency vehicle closely is dangerous -- it may stop or turn suddenly.",
                ("No, keep a safe distance behind it", true),
                ("Yes, to get through traffic faster", false),
                ("Yes, but only in heavy traffic", false),
                ("It doesn't matter", false)),
            Q("When must a driver use headlights?",
                SectionType.Rules, null,
                "Headlights are required whenever natural light isn't enough to see and be seen clearly.",
                ("From sunset to sunrise and in poor visibility (fog, heavy rain)", true),
                ("Only on freeways", false),
                ("Only when it is completely dark", false),
                ("Headlights are optional at all times", false)),
            Q("What does a solid double line in the centre of the road mean?",
                SectionType.Rules, null,
                "A solid double centre line means no overtaking is allowed from either direction.",
                ("Overtaking is prohibited in both directions", true),
                ("Overtaking is allowed if the road is clear", false),
                ("It marks a pedestrian crossing", false),
                ("It only applies to heavy vehicles", false)),
            Q("What does a single broken (dashed) centre line mean?",
                SectionType.Rules, null,
                "A dashed centre line means overtaking is permitted when it can be done safely.",
                ("Overtaking is permitted when it is safe to do so", true),
                ("Overtaking is never permitted", false),
                ("It marks a one-way road", false),
                ("It applies only to cyclists", false)),
            Q("When merging onto a freeway, who must give way?",
                SectionType.Rules, null,
                "A merging driver must fit into the existing flow of freeway traffic, not the other way round.",
                ("The merging driver gives way to traffic already on the freeway", true),
                ("Freeway traffic must give way to merging traffic", false),
                ("Whoever is travelling faster has right of way", false),
                ("Merging vehicles always have right of way", false)),
            Q("What must a driver do at a red traffic light?",
                SectionType.Rules, null,
                "A red light always means a full stop before the line, held until it turns green.",
                ("Stop completely before the stop line and wait for green", true),
                ("Slow down and proceed if the road is clear", false),
                ("Stop only if other traffic is present", false),
                ("Treat it the same as a yield sign", false)),
            Q("Why must high beam headlights be dimmed for oncoming traffic?",
                SectionType.Rules, null,
                "High beams can temporarily blind oncoming drivers, which is dangerous for everyone.",
                ("To avoid blinding the oncoming driver", true),
                ("To save fuel", false),
                ("It is only a courtesy, not a rule", false),
                ("High beams must never be dimmed", false)),
            Q("Is it legal to drive with a suspended or expired driving licence?",
                SectionType.Rules, null,
                "Driving without a valid licence is illegal regardless of prior driving experience.",
                ("No, it is illegal", true),
                ("Yes, for short trips only", false),
                ("Yes, if accompanied by a licensed driver", false),
                ("Only illegal if involved in a collision", false)),
            Q("How should a driver reverse on a public road?",
                SectionType.Rules, null,
                "Reversing is inherently risky -- keep it brief and check all around before and during.",
                ("Only for a short distance, with extreme care and full awareness of surroundings", true),
                ("As far as needed, at normal driving speed", false),
                ("Reversing on a public road is never allowed", false),
                ("Only at night", false)),
            Q("What should a driver do before pulling away from a parked position?",
                SectionType.Rules, null,
                "Checking mirrors and indicating before pulling off protects both you and other road users.",
                ("Check mirrors, indicate, and confirm it's safe before moving off", true),
                ("Pull away immediately once the engine starts", false),
                ("No checks are required if no cars are visible", false),
                ("Only check the mirror on the driver's side", false)),
            Q("Where may a vehicle not be parked?",
                SectionType.Rules, null,
                "Parking that blocks an intersection or driveway endangers and obstructs other road users.",
                ("In a way that obstructs an intersection or driveway", true),
                ("Anywhere there is space, regardless of visibility", false),
                ("Parking restrictions only apply to trucks", false),
                ("Parking restrictions only apply at night", false)),
            Q("May a driver overtake a vehicle that has stopped at a marked pedestrian crossing?",
                SectionType.Rules, null,
                "A stopped vehicle at a crossing may be giving way to a pedestrian you can't yet see -- never overtake it there.",
                ("No, overtaking there is not allowed", true),
                ("Yes, if no pedestrians are visible", false),
                ("Yes, if travelling slowly", false),
                ("Only motorcycles may overtake there", false)),
            Q("On a multi-lane road, where should slower-moving traffic keep, other than when overtaking?",
                SectionType.Rules, null,
                "Keeping left except when overtaking keeps faster lanes clear for passing traffic.",
                ("In the left-hand lane", true),
                ("In the right-hand lane", false),
                ("In whichever lane has less traffic", false),
                ("Lane position doesn't matter for slower traffic", false)),
            Q("What should a driver or passenger do before opening a car door?",
                SectionType.Rules, null,
                "A carelessly opened door can hit a passing cyclist or vehicle -- always check first.",
                ("Check for approaching traffic and cyclists", true),
                ("Open it quickly to avoid blocking the pavement", false),
                ("No check is needed if the car is parked", false),
                ("Only the driver needs to check, not passengers", false)),

            // Signs (15 more, all referencing a real, verified sign)
            Q("What does this road sign mean?", SectionType.Signs, "R6", null,
                ("Give Way / Yield to oncoming traffic", true), ("Stop", false),
                ("No entry", false), ("Keep Right", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R104", null,
                ("Keep Right", true), ("Keep Left", false),
                ("Turn Right", false), ("Proceed Straight", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R105", null,
                ("Turn Left", true), ("Turn Right", false),
                ("Keep Left", false), ("Proceed Straight", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R106", null,
                ("Turn Right", true), ("Turn Left", false),
                ("Keep Right", false), ("Roundabout", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R110", null,
                ("Pedestrians only", true), ("Cyclists only", false),
                ("Pedestrians prohibited", false), ("Motorcycles only", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R209", null,
                ("Left turn prohibited ahead", true), ("Right turn prohibited ahead", false),
                ("Left turn prohibited", false), ("U-turn prohibited", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R211", null,
                ("Left turn prohibited", true), ("Right turn prohibited", false),
                ("Left turn prohibited ahead", false), ("Overtaking prohibited", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R212", null,
                ("Right turn prohibited", true), ("Left turn prohibited", false),
                ("Right turn prohibited ahead", false), ("U-turn prohibited", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R214", null,
                ("Overtaking prohibited", true), ("Parking prohibited", false),
                ("Stopping prohibited", false), ("U-turn prohibited", false)),
            Q("What does this road sign mean?", SectionType.Signs, "R218", null,
                ("Pedestrians prohibited", true), ("Pedestrians only", false),
                ("Cyclists prohibited", false), ("Cyclists and pedestrians prohibited", false)),
            Q("What does this road sign mean?", SectionType.Signs, "W102", null,
                ("Crossroad ahead with priority", true), ("Crossroad ahead without priority", false),
                ("T-junction ahead", false), ("Roundabout ahead", false)),
            Q("What does this road sign mean?", SectionType.Signs, "W104", null,
                ("T-junction ahead", true), ("Crossroad ahead", false),
                ("Fork ahead", false), ("Side-road junction ahead", false)),
            Q("What does this road sign mean?", SectionType.Signs, "W115", null,
                ("Fork ahead", true), ("T-junction ahead", false),
                ("Crossroad ahead", false), ("Dual-carriageway begins ahead", false)),
            Q("What does this road sign mean?", SectionType.Signs, "W301", null,
                ("Traffic signal ahead", true), ("Stop control ahead", false),
                ("Give Way / Yield control ahead", false), ("Railway crossing ahead", false)),
            Q("What does this road sign mean?", SectionType.Signs, "W318", null,
                ("Railway crossing ahead", true), ("Tunnel ahead", false),
                ("Traffic signal ahead", false), ("Gate ahead", false)),

            // VehicleControls (6 more)
            Q("How should the steering wheel be held for full control of the vehicle?",
                SectionType.VehicleControls, null,
                "Hands on opposite sides of the wheel give the most control and the safest range of movement.",
                ("With hands placed on opposite sides of the wheel", true),
                ("With one hand only, resting on the gear lever", false),
                ("With both hands crossed over each other", false),
                ("Grip only matters when reversing", false)),
            Q("How should the accelerator be used for smooth driving?",
                SectionType.VehicleControls, null,
                "Smooth, progressive acceleration is safer and easier on the vehicle than sudden bursts of speed.",
                ("Applied smoothly and progressively, avoiding sudden acceleration", true),
                ("Pressed as hard as possible when moving off", false),
                ("Acceleration technique doesn't matter", false),
                ("Only relevant on a freeway", false)),
            Q("How should the brakes normally be applied?",
                SectionType.VehicleControls, null,
                "Smooth, progressive braking (except in a genuine emergency) is safer and more comfortable for everyone in the vehicle.",
                ("Smoothly and progressively, except in a genuine emergency", true),
                ("As hard as possible every time", false),
                ("Braking technique doesn't matter at low speed", false),
                ("Only the handbrake should be used to slow down", false)),
            Q("What is the purpose of the clutch pedal in a manual vehicle?",
                SectionType.VehicleControls, null,
                "The clutch disengages the engine from the gearbox so gears can be changed, or the car can stop, without stalling.",
                ("To disengage the engine from the gearbox for gear changes or stopping", true),
                ("To control the vehicle's speed directly", false),
                ("To operate the brake lights", false),
                ("It has no function once the vehicle is moving", false)),
            Q("Besides checking mirrors, what else should a driver do before changing lanes?",
                SectionType.VehicleControls, null,
                "Mirrors don't show everything -- a quick glance over the shoulder checks the blind spot.",
                ("Check the blind spot with a glance over the shoulder", true),
                ("Nothing else is necessary if mirrors were checked", false),
                ("Sound the hooter instead", false),
                ("Flash the headlights instead", false)),
            Q("What should a driver do if a tyre bursts while driving?",
                SectionType.VehicleControls, null,
                "Sudden hard braking or sharp steering after a blowout can cause loss of control -- ease off gradually instead.",
                ("Grip the wheel firmly, ease off the accelerator gradually, and avoid harsh braking", true),
                ("Brake as hard as possible immediately", false),
                ("Steer sharply to the side of the road", false),
                ("Accelerate to reach a safe stopping point faster", false)),
        };

        await _context.Questions.AddRangeAsync(questions);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} additional starter questions.", questions.Count);
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
