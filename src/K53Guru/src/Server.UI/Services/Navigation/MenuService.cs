using K53Guru.Application.Common.Constants;
using K53Guru.Server.UI.Models.NavigationMenu;

namespace K53Guru.Server.UI.Services.Navigation;

public class MenuService : IMenuService
{
    private readonly List<MenuSectionModel> _features = new()
    {
        new MenuSectionModel
        {
            Title = "Application",
            SectionItems = new List<MenuSectionItemModel>
            {
                new() { Title = "Home", Icon = Icons.Material.Filled.Home, Href = "/" },
                // E-Commerce (Products/Documents/Contacts), Analytics, Banking, and Booking were
                // the starter template's generic demo/stub modules -- unrelated to K53Guru's
                // domain and removed from the nav per explicit request. Their pages/backing code
                // were intentionally left in place rather than deleted (see chat for scope); ask
                // if you also want them physically removed.
            }
        },
        new MenuSectionModel
        {
            Title = "MANAGEMENT",
            Roles = new[] { Roles.Admin },
            SectionItems = new List<MenuSectionItemModel>
            {
                new()
                {
                    IsParent = true,
                    Title = "Authorization",
                    Icon = Icons.Material.Filled.ManageAccounts,
                    MenuItems = new List<MenuSectionSubItemModel>
                    {
                        new()
                        {
                            Title = "Multi-Tenant",
                            Href = "/system/tenants",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Users",
                            Href = "/identity/users",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Roles",
                            Href = "/identity/roles",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Profile",
                            Href = "/user/profile",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Login History",
                            Href = "/pages/identity/loginaudits",
                            PageStatus = PageStatus.Completed
                        },
                    }
                },
                new()
                {
                    IsParent = true,
                    Title = "System",
                    Icon = Icons.Material.Filled.Devices,
                    MenuItems = new List<MenuSectionSubItemModel>
                    {
                        // "Picklist" (generic key/value list management from the starter
                        // template) removed from the nav per explicit request -- not part of
                        // K53Guru's domain. Page/backing code left in place; see chat for scope.
                        new()
                        {
                            Title = "Road Signs",
                            Href = "/system/roadsigns",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Questions",
                            Href = "/system/questions",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Tests",
                            Href = "/system/tests",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Audit Trails",
                            Href = "/system/audittrails",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Email Templates",
                            Href = "/pages/system/email-templates",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Logs",
                            Href = "/system/logs",
                            PageStatus = PageStatus.Completed
                        },
                        new()
                        {
                            Title = "Jobs",
                            Href = "/jobs",
                            PageStatus = PageStatus.Completed,
                            Target = "_blank"
                        }
                    }
                }
            }
        }
    };

    public IEnumerable<MenuSectionModel> Features => _features;
}
