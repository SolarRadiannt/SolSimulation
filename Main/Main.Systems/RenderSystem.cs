namespace SolSimulation.Main.Systems;


using fennecs;
using SolTK;

public interface IShape;

public record struct Circle(float Radius) : IShape;

public readonly struct Shape<T> where T : IShape
{
    public readonly T Value { get; init; }
}



public class RenderSystem : ISystem, IProcessable
{
    private static World _world = ECS.World;
    private static Stream<Shape<Circle>> _renderCircle = _world.Stream<Shape<Circle>>();
    private static void RenderCircle() =>
    _renderCircle.For(static (in entity, ref shape) =>
    {
        
    });

    public void ApplyCircle(Entity ent, float radius) =>
        ent.Add(new Shape<Circle>
        {
            Value = new Circle(radius)
        });

    public int Priority => STKPriority.Lowest;
    public void Init()
    {
        Scheduler.Register(this);
    }

    public void Process(double _)
    {
        RenderCircle();
    }
}