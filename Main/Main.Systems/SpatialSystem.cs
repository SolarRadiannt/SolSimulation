namespace SolSimulation.Main.Systems;

using System;
using System.Collections.Generic;
using fennecs;
using Godot;
using SolTK;
using SolTK.CommonUtil;
using SolTK.Spatials;


public struct OverlapParams()
{
    public int? MaxResults = null;
}

public struct RaycastParams()
{
    
}

public readonly struct RaycastResult
{
    public readonly Vec2 Position;
    public readonly Vec2 Normal;
    public readonly Entity Entity;
    public readonly Collider collider;
    public readonly float Distance;
}

public class SpatialSystem  : ISystem, IProcessable
{
    private static readonly World _world = ECS.World;
    private readonly SpatialGrid _grid = new(24);
    
    private readonly Stream<Transform2D, Collider> _queryAble =
            _world.Stream<Transform2D, Collider>();
    private void InsertToGrid(in EntityRef entity, ref Transform2D transform, ref Collider collider)
    {
        _grid.Insert(entity.Entity, collider.Shape.GetAABB(transform));
    }

    public bool QueryBroadAABB(
        AABB queryAABB,
        List<Entity> results,
        OverlapParams? spatialParams = null
    )
    {
        results.Clear();
        _grid.QueryAABB(queryAABB, results, spatialParams?.MaxResults);
        return results.Count > 0;
    }

    public bool Raycast(
        Vec2 origin,
        Vec2 direction,
        RaycastParams? spatialParams,
        out RaycastResult result
    )
    {
        throw new UnimplementedException();
    }

    public bool Raycast(
        Vec2 origin,
        Vec2 direction,
        out RaycastResult result
    ) => Raycast(origin, direction, null, out result);

    public int Priority => STKPriority.Applying - 10;
    public void Init()
    {
        Scheduler.RegisterPhysics(this);
    }

    public void Process(double dt)
    {
        _grid.Clear();
        _queryAble.For(InsertToGrid);
    }
}