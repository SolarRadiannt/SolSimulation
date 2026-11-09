namespace SolSimulation.Main.Systems;

using fennecs;
using Godot;
using SolTK;
using SolTK.CommonUtil;

public readonly record struct EntityMovedEvent(
    Vec2 LastPosition,
    Vec2 NewPosition
);

public struct Motion(float speed)
{
    public float MoveSpeed = speed;
    public Vec2 MoveDirection {get; readonly set => value.Normalized();} = Vec2.Zero;
    public readonly Vec2 MoveVelocity => MoveDirection * MoveSpeed;

    public Vec2 VelocityInfluence = Vec2.Zero;
    public readonly Vec2 Velocity => MoveVelocity + VelocityInfluence;
}

public class MovementSystem : ISystem, IProcessable
{
    private readonly static World _world = ECS.World;
    private readonly static Stream<Transform2D, Motion> _moveables =
        _world.Stream<Transform2D, Motion>();
    private static void MoveEntity(
        float dt,
        in EntityRef entRef,
        ref Transform2D transform,
        ref Motion motion
    )
    {
        var oldPos = transform.Origin;
        var velocity = motion.Velocity;
        if (velocity == Vec2.Zero) return;

        transform.Origin += velocity * dt;

        if (oldPos != transform.Origin)
        {
            var moveEvent = new EntityMovedEvent(oldPos, transform.Origin);
            EventBus.RaiseLocal(entRef.Entity, ref moveEvent);
        }
    }

    public int Priority => STKPriority.Applying;
    public void Init()
    {
        Scheduler.Register(this);
    }
    public void Process(double dt)
    {
        _moveables.For((float)dt, MoveEntity);
    }
}