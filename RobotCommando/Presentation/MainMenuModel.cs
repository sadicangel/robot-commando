namespace RobotCommando.Presentation;

public partial class MainMenuModel
{
    private readonly IAdventureService _adventureService;
    private readonly INavigator _navigator;

    public MainMenuModel(IAdventureService adventureService, INavigator navigator)
    {
        _adventureService = adventureService;
        _navigator = navigator;
    }

    public string Title => "Robot Commando";

    public string Subtitle => "Wake Thalos, reclaim its robots, and push through the invasion.";

    public bool CanExit =>
        OperatingSystem.IsWindows()
        || OperatingSystem.IsLinux()
        || OperatingSystem.IsMacOS()
        || OperatingSystem.IsMacCatalyst();

    public async Task StartAdventure()
    {
        await _adventureService.StartNewGame();
        await _navigator.NavigateViewModelAsync<AdventureModel>(this);
    }

    public Task ExitGame()
    {
        if (CanExit)
        {
            Application.Current.Exit();
        }

        return Task.CompletedTask;
    }
}
