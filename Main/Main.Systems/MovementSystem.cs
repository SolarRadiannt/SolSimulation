namespace SolSimulation.Main.Systems;

using System;
using fennecs;
using Godot;
using SolTK;

public struct CustomMoveTo;
public record struct MoveTo(
    Vec2 Goal,
    float? ReachDistance = null
);
public readonly record struct EntityMoveToReachedEvent(
    Vec2 Goal
);

public readonly record struct EntityMovedEvent(
    Vec2 LastPosition,
    Vec2 NewPosition
);

public struct Motion(float speed)
{
    public float MoveSpeed = speed;
    private Vec2 _moveDir = Vec2.Zero;
    public Vec2 MoveDirection
    {
        readonly get => _moveDir;
        set {
            _moveDir = value.Normalized();
        }
    }
    
    public readonly Vec2 MoveVelocity => MoveDirection * MoveSpeed;

    public Vec2 VelocityInfluence = Vec2.Zero;
    public readonly Vec2 Velocity => MoveVelocity + VelocityInfluence;
}

public class MovementSystem : ISystem
{
    private readonly static float REACHED_DIST = 1;
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

    private readonly static Stream<Transform2D, MoveTo, Motion> _moveToAbles =
        _world.Query<Transform2D, MoveTo, Motion>()
            .Not<CustomMoveTo>()
            .Stream();
    private static void EntityMoveTo(
        in EntityRef entityRef, 
        ref Transform2D transform,
        ref MoveTo moveTo,
        ref Motion motion
    )
    {
        var resultant =  moveTo.Goal - transform.Origin;
        float distSq = resultant.LengthSquared();
        float reachDist = moveTo.ReachDistance ?? REACHED_DIST;
        
        if (distSq <= reachDist * reachDist)
        {
            entityRef.Remove<MoveTo>();
            motion.MoveDirection = Vec2.Zero;

            var reachedEvent = new EntityMoveToReachedEvent(moveTo.Goal);
            EventBus.RaiseLocal(entityRef.Entity, ref reachedEvent);
            return;
        }

        motion.MoveDirection = resultant / MathF.Sqrt(distSq);
    }

    public void Init()
    {
        Scheduler.RegisterPhysics(
            static _ => _moveToAbles.For(EntityMoveTo),
            STKPriority.Transformation
        );
        Scheduler.RegisterPhysics(
            static dt => _moveables.For((float)dt, MoveEntity),
            STKPriority.Applying
        );
    }
}