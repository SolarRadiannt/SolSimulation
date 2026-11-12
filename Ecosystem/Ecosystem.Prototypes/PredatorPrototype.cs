namespace SolSimulation.Ecosystem.Prototypes;

using fennecs;
using Godot;
using SolSimulation.Main.Systems;
using SolTK;
using SolTK.Spatials;

public struct Predator;

public class PredatorPrototype : IService
{
    private static readonly World _world = ECS.World;
    private static readonly Shape _shape = Shape.Circle(10);

    private static readonly float _baseSpeed = 440;
    private static readonly float _baseTurnSpeed = 20;
    
    public static readonly EntityTemplate<Transform2D> Template =
        _world.Template()
            .Add<Predator>()
            .Add<Name>("Predator")

            .Add(new Stomach(100))

            .Add(new Collider(_shape))
            .Add(new RenderDesc(_shape, new("#ff5e00")))
            .Add(new Motion(_baseSpeed))
            .Add(new MoveAutoRotate(_baseTurnSpeed))
            .Needs<Transform2D>();
    
    public void Init()
    {
        
    }
}