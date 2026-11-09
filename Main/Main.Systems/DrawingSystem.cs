namespace SolSimulation.Main.Systems;

using Godot;
using SolTK;



public class DrawingSystem : ISystem, IProcessable
{
    private void SpawnBlob(Vec2 position) =>
        ECS.Entity("Hi")
			.Add(new Transform2D(0, position))
			.Add(new RenderDesc(
				SolTK.Spatials.Shape.Circle(10)
			));
    
    public int Priority => STKPriority.Action;
    public void Init()
    {
        Scheduler.Register(this);
    }

    public void Process(double _)
    {
        var mousePos = Main.Instance.GetGlobalMousePosition();
        if (Input.IsActionPressed("SpawnHi"))
            SpawnBlob(mousePos);
    }
}