using K53Guru.Server.UI.Models.NavigationMenu;

namespace K53Guru.Server.UI.Services.Navigation;

public interface IMenuService
{
    IEnumerable<MenuSectionModel> Features { get; }
}
