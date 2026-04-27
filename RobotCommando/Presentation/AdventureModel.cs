using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RobotCommando.Presentation;

public partial class AdventureModel : INotifyPropertyChanged
{
    private readonly IAdventureService _adventureService;
    private readonly INavigator? _navigator;
    private AdventureViewState _state;

    public AdventureModel(IAdventureService adventureService, INavigator? navigator = null)
    {
        _adventureService = adventureService;
        _navigator = navigator;
        _state = adventureService.Snapshot;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AdventureViewState State
    {
        get => _state;
        private set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            OnPropertyChanged();
        }
    }

    public async Task SelectChoice(string actionId)
    {
        await _adventureService.SelectChoice(actionId);
        Refresh();

        if (!State.HasSession && _navigator is not null)
        {
            await _navigator.NavigateViewModelAsync<MainMenuModel>(this);
        }
    }

    public async Task ExecuteInteraction(string actionId)
    {
        if (actionId.StartsWith("item:", StringComparison.Ordinal))
        {
            await _adventureService.PickUpItem(actionId);
        }
        else if (actionId.StartsWith("robot:", StringComparison.Ordinal))
        {
            await _adventureService.TakeRobot(actionId);
        }
        else if (actionId.StartsWith("enemy:", StringComparison.Ordinal) || actionId.StartsWith("monster:", StringComparison.Ordinal))
        {
            await _adventureService.Fight(actionId);
        }
        else
        {
            throw new InvalidOperationException($"Unknown interaction '{actionId}'.");
        }

        Refresh();
    }

    public async Task Escape(string actionId)
    {
        await _adventureService.Escape(actionId);
        Refresh();
    }

    private void Refresh() => State = _adventureService.Snapshot;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
