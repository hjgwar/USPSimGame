using Microsoft.AspNetCore.Components;
using USPSimGame.Data.Entities;
using USPSimGame.Services;

namespace USPSimGame.Components.Pages.Creator;

public partial class CreatorTeams : ComponentBase, IDisposable
{
    [Inject]
    public CreatorAuthState AuthState { get; set; } = default!;

    protected override void OnInitialized()
    {
        AuthState.OnStateChanged += StateHasChanged;
    }

    protected void HandleLoginSuccess(User user)
    {
        AuthState.LogIn(user);
    }

    public void Dispose()
    {
        AuthState.OnStateChanged -= StateHasChanged;
    }
}
