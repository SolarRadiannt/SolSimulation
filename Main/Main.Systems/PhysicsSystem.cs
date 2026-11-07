namespace SolSimulation.Main.Systems;

using System.Collections.Generic;
using fennecs;

using SolTK;
using SolTK.CommonUtil;

using AetherWorld = nkast.Aether.Physics2D.Dynamics.World;
using AetherBody = nkast.Aether.Physics2D.Dynamics.Body;
using AetherBodyType = nkast.Aether.Physics2D.Dynamics.BodyType;
using AetherVertices = nkast.Aether.Physics2D.Common.Vertices;
using nkast.Aether.Physics2D.Collision.Shapes;
using SolTK.Components;

public readonly struct BodyHandle(Entity entity, AetherBody body)
{
    readonly public Entity Entity = entity;
    readonly internal AetherBody _body = body;

    public Vec2 Position {
        get
        {
            var pos = _body.Position;

            return new Vec2(pos.X, pos.Y);
        }
        set
        {
            _body.Position = new AVec2(value.X, value.Y);
        }
    }
}

public struct BodyParams()
{
    public Vec2 Position;
    public Vec2 Size;
    
    public float Rotation = 0;
}

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
    public readonly BodyHandle Body;
    public readonly float Distance;
}

public class PhysicsSystem : ISystem, IProcessable
{
    private static readonly World _world = ECS.World;
    private readonly AetherWorld _physicsWorld = new();
    private void StepWorld(float dt) => _physicsWorld.Step(dt);

    private readonly Stream<BodyHandle, MarkDespawn> _despawnedWithBody =
        _world.Stream<BodyHandle, MarkDespawn>();
    private void FlushDespawned(double _) =>
        _despawnedWithBody.For(
        (ref body, ref _) =>
            _physicsWorld.Remove(body._body)
        );

    public void ApplyKinBody(Entity entity, BodyParams bodyParams)
    {
        var body = _physicsWorld.CreateBody(
            bodyParams.Position.ToAVec2(),
            bodyParams.Rotation,
            AetherBodyType.Kinematic
        );
        
        body.Tag = entity;
        AddBoxFixture(body, bodyParams.Size);
        entity.Add(new BodyHandle(entity, body));
    }

    private static void AddBoxFixture(AetherBody body, Vec2 size)
    {
        // Size defaults to (0,0) and Aether rejects degenerate polygons, so stay fixtureless instead.
        if (size.X <= 0f || size.Y <= 0f) return;

        var halfX = size.X * 0.5f;
        var halfY = size.Y * 0.5f;

        // Aether wants half extents and counter-clockwise winding; BodyParams.Size is full extents.
        var vertices = new AetherVertices(new AVec2[]
        {
            new(-halfX, -halfY),
            new( halfX, -halfY),
            new( halfX,  halfY),
            new(-halfX,  halfY),
        });
        vertices.ForceCounterClockWise();

        // Density lives on the shape; a Kinematic body has infinite mass so it is ignored here.
        body.CreateFixture(new PolygonShape(vertices, 1f));
    }

    public bool GetEntitiesInBox(
        Vec2 origin,
        Vec2 size,
        List<Entity> result,
        OverlapParams spatialParams
    )
    {
        throw new UnimplementedException();
    }

    public bool GetEntitiesInRadius(
        Vec2 position,
        float radius,
        List<Entity> result,
        OverlapParams spatialParams
    )
    {
        throw new UnimplementedException();
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

    public int Priority => STKPriority.Applying;
    public void Init()
    {
        Scheduler.Register(FlushDespawned, STKPriority.Flush + 10);
        Scheduler.RegisterPhysics(this);
    }
    public void Process(double dt)
    {
        StepWorld((float)dt);
    }
}