namespace SolSimulation.Main.Systems;
using fennecs;
using Godot;
using SolTK;
using SolTK.Spatials;

public struct Prey;


public class PreySystem : IService
{
    private static readonly World _world = ECS.World;
    
    private static readonly Shape _preyShape = Shape.Circle(10);
    private static readonly Collider _preyCollider = new(_preyShape);
    private static readonly RenderDesc _preyRenderDesc = new(_preyShape, new Color("#0000ff"));

    public static readonly EntityTemplate<Transform2D, Motion> PreyTemplate =
        _world.Template()
            .Add("Prey")
            .Add<Name>("Prey")
            .Add(_preyCollider)
            .Add(_preyRenderDesc)
            .Needs<Transform2D>()
            .Needs<Motion>();

    public void Init()
    {
        
    }
}