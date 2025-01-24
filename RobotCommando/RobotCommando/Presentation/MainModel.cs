namespace RobotCommando.Presentation;

public partial record MainModel
{
    private INavigator _navigator;

    public MainModel(
        IStringLocalizer localizer,
        IOptions<AppConfig> appInfo,
        INavigator navigator)
    {
        _navigator = navigator;
        Title = "Main";
        Title += $" - {localizer["ApplicationName"]}";
        Title += $" - {appInfo?.Value?.Environment}";
    }

    public string? Title { get; }

    public IState<Player> Player => State<Player>.Value(this, () => new Player("Player"));

    public async ValueTask Roll()
    {
        await Player.UpdateAsync(player => new Player(player?.Name ?? "Player"));
    }

    public async ValueTask Start() =>
        await _navigator.NavigateViewModelAsync<SecondModel>(this, data: await Player);

}
