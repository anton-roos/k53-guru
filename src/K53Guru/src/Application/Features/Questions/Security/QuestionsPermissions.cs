using System.ComponentModel;

namespace K53Guru.Application.Common.Security;

public static partial class Permissions
{
    [DisplayName("Question Permissions")]
    [Description("Set permissions for authoring and editing questions")]
    public static class Questions
    {
        [Description("Allows viewing the question list")]
        public const string View = "Permissions.Questions.View";

        [Description("Allows creating new questions")]
        public const string Create = "Permissions.Questions.Create";

        [Description("Allows editing existing questions")]
        public const string Edit = "Permissions.Questions.Edit";
    }
}

public class QuestionsAccessRights
{
    public bool View { get; set; }
    public bool Create { get; set; }
    public bool Edit { get; set; }
}
