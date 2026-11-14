namespace SolSimulation.Main.Systems;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using fennecs;
using Godot;
using SolTK;
using SolTK.Spatials;

public struct PlayerControl;

public class PlayerSystem : IService
{
    private readonly static World _world = ECS.World;
    private readonly static Dictionary<StringName, Vec2> _moveNormals = new()
    {
        ["MoveUp"] = Vec2.Up,
        ["MoveDown"] = Vec2.Down,
        ["MoveLeft"] = Vec2.Left,
        ["MoveRight"] = Vec2.Right,
    };

    private static Vec2 GetMoveDirection()
    {
        var normal = Vec2.Zero;
        foreach (var name in _moveNormals.Keys.Where(name => Input.IsActionPressed(name)))
            normal += _moveNormals[name];
        
        return normal.Normalized();
    }

    private readonly static FilteredStream<Motion> _playerMoveables = 
        _world.Stream<Motion>()
            .Has(Comp<PlayerControl>.Plain);
    public static void ControlMovement(ref Motion motion)
    {
        motion.MoveDirection = GetMoveDirection();
    }

    public void Init()
    {
        Scheduler.RegisterSimulation(
            static _ => _playerMoveables.For(ControlMovement),
            STKPriority.Transformation
        );
        
        ECS.Entity("Player")
            .Add(new Motion(400))
            .Add(new RenderDesc(Shape.Circle(10), new("0056d2")))
            .Add(Main.Instance.GetNode<Marker2D>("PlayerSpawn").Transform)
            .Add<PlayerControl>()
            .Add(new MoveAutoRotate());
    }
}