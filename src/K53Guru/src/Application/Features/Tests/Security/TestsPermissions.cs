using System.ComponentModel;

namespace K53Guru.Application.Common.Security;

public static partial class Permissions
{
    [DisplayName("Test Permissions")]
    [Description("Set permissions for organising questions into tests")]
    public static class Tests
    {
        [Description("Allows viewing the test list")]
        public const string View = "Permissions.Tests.View";

        [Description("Allows creating new tests")]
        public const string Create = "Permissions.Tests.Create";

        [Description("Allows editing existing tests")]
        public const string Edit = "Permissions.Tests.Edit";

        [Description("Allows publishing and unpublishing tests")]
        public const string Publish = "Permissions.Tests.Publish";
    }
}

public class TestsAccessRights
{
    public bool View { get; set; }
    public bool Create { get; set; }
    public bool Edit { get; set; }
    public bool Publish { get; set; }
}
