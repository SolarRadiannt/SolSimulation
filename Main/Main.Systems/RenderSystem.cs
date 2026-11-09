namespace SolSimulation.Main.Systems;

using System.Windows.Markup;
using fennecs;
using Godot;
using SolTK;
using SolTK.Spatials;

public struct RenderDesc(Shape shape, Color? color = null)
{
    public readonly static Color DEFAULT_COLOR =
        new((float)0.847, (float)0.078, (float)0.0, (float)0.996);
    public Shape Shape = shape;
    public Color Color = color ?? DEFAULT_COLOR;
    public bool AntiLiased = false;
}

public class RenderSystem : ISystem, IProcessable
{
    private static readonly World _world = ECS.World;
    private readonly static Stream<Transform2D, RenderDesc> _renderables =
        _world.Stream<Transform2D, RenderDesc>();
    private Rid _canvasItem;
    private void Render(ref Transform2D transform, ref RenderDesc renderDesc)
    {
        var center = transform.Origin;  

        switch (renderDesc.Shape.Type)
        {
            case Shape.Kind.Circle:
                RenderingServer.CanvasItemAddCircle(
                    _canvasItem,
                    center,
                    renderDesc.Shape.Radius,
                    renderDesc.Color
                );
                break;
            
            case Shape.Kind.Box:
                var size = new Vec2(
                    renderDesc.Shape.HalfExtents.X * 2, 
                    renderDesc.Shape.HalfExtents.Y * 2
                );
                var rect = new Rect2(center - size / 2f, size);
                RenderingServer.CanvasItemAddRect(
                    _canvasItem,
                    rect,
                    renderDesc.Color,
                    renderDesc.AntiLiased
                );
                break;
        }
    }

    public int Priority => STKPriority.Lowest;
    public void Init()
    {
        _canvasItem = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(
            _canvasItem, 
            Main.Instance.GetCanvasItem()
        );

        Scheduler.Register(this);
    }

    public void Process(double _)
    {
        RenderingServer.CanvasItemClear(_canvasItem);
        _renderables.For(Render);
    }
}