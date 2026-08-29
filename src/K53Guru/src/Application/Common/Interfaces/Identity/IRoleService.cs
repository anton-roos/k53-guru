using K53Guru.Application.Features.Identity.DTOs;

namespace K53Guru.Application.Common.Interfaces.Identity;

public interface IRoleService
{
    List<ApplicationRoleDto> DataSource { get; }
    event Func<Task>? OnChange;
    Task InitializeAsync();
    Task  RefreshAsync();
}
