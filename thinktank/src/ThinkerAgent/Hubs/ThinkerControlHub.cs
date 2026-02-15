using Microsoft.AspNetCore.SignalR;
using ThinkerAgent.Services;

namespace ThinkerAgent.Hubs;

public sealed class ThinkerControlHub : Hub
{
    private readonly WorkerState _state;

    public ThinkerControlHub(WorkerState state)
    {
        _state = state;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("WorkerStateChanged", _state.GetSnapshot());
        await base.OnConnectedAsync();
    }

    public WorkerStateSnapshot RequestCurrentState() => _state.GetSnapshot();
}
