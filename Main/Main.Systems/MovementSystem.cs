namespace SolSimulation.Main.Systems;

using System;
using fennecs;
using Godot;
using SolTK;

public struct CustomMoveTo;
public struct CustomAutoRotate;

public record struct MoveAutoRotate(
    float TurnRate = 5f
);
public record struct MoveTo(
    Vec2 Goal,
    float ReachDistance = 1
);
public readonly record struct EntityMoveToReachedEvent(
    Vec2 Goal
);

public readonly record struct EntityMovedEvent(
    Vec2 LastPosition,
    Vec2 NewPosition
);

public struct Motion(float moveSpeed = 400)
{
    public float MoveSpeed = moveSpeed;
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

public class MovementSystem : IService
{
    private readonly static World _world = ECS.World;
    private readonly static FilteredStream<Transform2D, Motion> _moveables =
        _world.Stream<Transform2D, Motion>()
        .Where(static (in Motion motion) => !motion.Velocity.IsZeroApprox());
    private static void MoveEntity(
        float dt,
        in EntityRef entRef,
        ref Transform2D transform,
        ref Motion motion
    )
    {
        var oldPos = transform.Origin;
        var velocity = motion.Velocity;

        transform.Origin += velocity * dt;

        if (oldPos != transform.Origin)
        {
            var moveEvent = new EntityMovedEvent(oldPos, transform.Origin);
            EventBus.RaiseLocal(entRef.Entity, ref moveEvent);
        }
    }
    
    private readonly static FilteredStream<Transform2D, MoveTo, Motion> _moveToAbles =
        _world.Stream<Transform2D, MoveTo, Motion>()
            .Not(Comp<CustomMoveTo>.Plain);
    private static void EntityMoveTo(
        in EntityRef entityRef, 
        ref Transform2D transform,
        ref MoveTo moveTo,
        ref Motion motion
    )
    {
        var resultant =  moveTo.Goal - transform.Origin;
        float distSq = resultant.LengthSquared();
        float reachDist = moveTo.ReachDistance;
        
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

    private readonly static FilteredStream<Transform2D, Motion, MoveAutoRotate> _moveAutoRotateAbles =
        _world.Stream<Transform2D, Motion, MoveAutoRotate>()
            .Not(Comp<CustomAutoRotate>.Plain)
            .Where(static (in Motion motion) => !motion.MoveDirection.IsZeroApprox());
    private static void MoveAutoRotate(
        float dt,
        ref Transform2D transform,
        ref Motion motion,
        ref MoveAutoRotate moveAutoRotate
    )
    {
        float targetAngle = motion.MoveDirection.Angle();

        float angleDiff = Mathf.AngleDifference(transform.Rotation, targetAngle);
        
        float step = moveAutoRotate.TurnRate * dt;
        float newDiff = Mathf.MoveToward(angleDiff, 0f, step);
        
        float newAngle = transform.Rotation + newDiff;
        transform = new(newAngle, transform.Origin);
    }

    public void Init()
    {
        Scheduler.RegisterPhysics(
            static _ => _moveToAbles.For(EntityMoveTo),
            STKPriority.Transformation
        );

        Scheduler.RegisterPhysics(
            static dt => _moveAutoRotateAbles.For((float)dt, MoveAutoRotate),
            STKPriority.Transformation - 1 // lowest runs last
        );

        Scheduler.RegisterPhysics(
            static dt => _moveables.For((float)dt, MoveEntity),
            STKPriority.Applying
        );
    }
}