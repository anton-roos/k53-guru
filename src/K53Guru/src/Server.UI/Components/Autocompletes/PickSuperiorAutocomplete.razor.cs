using K53Guru.Application.Common.Interfaces.Identity;
using K53Guru.Application.Features.Identity.DTOs;
using K53Guru.Infrastructure.Services.Identity;

namespace K53Guru.Server.UI.Components.Autocompletes;
#nullable disable warnings
public class PickSuperiorAutocomplete<T> : MudAutocomplete<ApplicationUserDto>
{
    public PickSuperiorAutocomplete()
    {
        SearchFunc = SearchKeyValues;
        ToStringFunc = dto => dto?.UserName;
        Clearable = true;
        Dense = true;
        ResetValueOnEmptyText = true;
        ShowProgressIndicator = true;
        MaxItems = 200;
    }
    [Parameter] public string? TenantId { get; set; }
    [Parameter] public string? OwnerName { get; set; }

    [Inject] private IDataSourceService<ApplicationUserDto> UserService { get; set; } = default!;

    private Task<IEnumerable<ApplicationUserDto>> SearchKeyValues(string? value, CancellationToken cancellation)
    {
        var result = UserService.DataSource.Where(x =>
            (x.TenantId != null && x.TenantId.Equals(TenantId) || TenantId == null) && !x.UserName.Equals(OwnerName));
        if (!string.IsNullOrWhiteSpace(value))
            result = UserService.DataSource.Where(x => (x.TenantId != null && x.TenantId.Equals(TenantId) || TenantId == null) && !x.UserName.Equals(OwnerName) &&
                                                       (x.UserName.Contains(value,
                                                            StringComparison.OrdinalIgnoreCase) ||
                                                        x.Email.Contains(value, StringComparison.OrdinalIgnoreCase)));
        return Task.FromResult(result);
    }
    protected override void OnInitialized()
    {
        UserService.OnChange += TenantsService_OnChange;
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await UserService.InitializeAsync();
        }

    }
    private async Task TenantsService_OnChange()
    {
        await InvokeAsync(StateHasChanged);
    }
    protected override async ValueTask DisposeAsyncCore()
    {
        UserService.OnChange -= TenantsService_OnChange;
        await base.DisposeAsyncCore();
    }
}
