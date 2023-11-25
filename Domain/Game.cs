namespace RobotCommando;

public sealed record class Game(Die Die, Player Player, Inventory Inventory, Robot? Robot, Page Page)
{

}