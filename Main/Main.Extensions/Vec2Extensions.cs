namespace SolSimulation.Main.Extensions;

public static class Vec2Extensions
{
    public static AVec2 ToAVec2(this Vec2 self) => new(self.X, self.Y);
}