namespace SolSimulation.Ecosystem.Prototypes;

using fennecs;
using Godot;
using SolSimulation.Main.Systems;
using SolTK;
using SolTK.Spatials;

public struct Food;

public static class FoodPrototype
{
    private static readonly World _world = ECS.World;
    private static readonly Shape _shape = Shape.Circle(10);

    public static readonly EntityTemplate<Transform2D> Template =
        _world.Template()
            .Add<Food>()
            .Add<Name>("Food")
            .Add(new Collider(_shape))
            .Add(new RenderDesc(_shape, new("#02ff17")))
            .Needs<Transform2D>();
}