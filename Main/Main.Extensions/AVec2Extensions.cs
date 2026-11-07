namespace SolSimulation.Main.Extensions;

public static class AVec2Extensions
{
    public static Vec2 ToVec2(this AVec2 self) => new(self.X, self.Y);
}