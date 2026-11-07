namespace SolSimulation.Main.Systems;

using System.Collections.Generic;
using fennecs;
using Godot;

using nkast.Aether.Physics2D.Dynamics;

using SolTK;
using SolTK.CommonUtil;

public struct OverlapParams
{
    public int MaxResult;
}

public struct RaycastParams
{
    
}

public readonly struct RaycastResult
{
    public readonly Vector2 Position;
    public readonly Vector2 Normal;
    public readonly Entity Hit;
    public readonly Body Body;
}

public class PhysicsSystem : ISystem, IProcessable
{
    public void ApplyBody(Entity entity, Transform2D origin, Vector2 size)
    {
        throw new UnimplementedException();
    }

    //             if it does not support transform just use Vec2
    public bool GetEntitiesInBox(
        Transform2D origin,
        Vector2 size,
        List<Entity> result,
        OverlapParams spatialParams
    )
    {
        throw new UnimplementedException();
    }

    public bool GetEntitiesInRadius(
        Vector2 position,
        float radius,
        List<Entity> result,
        OverlapParams spatialParams
    )
    {
        throw new UnimplementedException();
    }

    public bool Raycast(
        Vector2 origin,
        Vector2 direction,
        RaycastParams? spatialParams,
        out RaycastResult? result
    )
    {
        throw new UnimplementedException();
    }

    public bool Raycast(
        Vector2 origin,
        Vector2 direction,
        out RaycastResult? result
    ) => Raycast(origin, direction, null, out result);

    public int Priority => STKPriority.Lowest;
    public void Init()
    {
        Scheduler.RegisterPhysics(this);
    }

    public void Process(double _)
    {
        
    }
}