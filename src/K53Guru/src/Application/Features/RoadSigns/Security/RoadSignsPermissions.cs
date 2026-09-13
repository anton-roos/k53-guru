using System.ComponentModel;

namespace K53Guru.Application.Common.Security;

public static partial class Permissions
{
    [DisplayName("Road Sign Permissions")]
    [Description("Set permissions for the road sign catalog")]
    public static class RoadSigns
    {
        [Description("Allows viewing the road sign catalog")]
        public const string View = "Permissions.RoadSigns.View";

        [Description("Allows creating new road signs")]
        public const string Create = "Permissions.RoadSigns.Create";

        [Description("Allows editing existing road signs")]
        public const string Edit = "Permissions.RoadSigns.Edit";
    }
}

public class RoadSignsAccessRights
{
    public bool View { get; set; }
    public bool Create { get; set; }
    public bool Edit { get; set; }
}
