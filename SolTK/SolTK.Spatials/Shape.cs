namespace SolTK.Spatials;

using System;
using System.Diagnostics;
using Godot;

public readonly struct Shape
{
    public enum Kind : byte { Circle, Box }

    private readonly Kind _kind;
    private readonly float _radius;     // Circle only
    private readonly Vec2  _halfExtents; // Box only

    private Shape(Kind kind, float radius, Vec2 halfExtents)
    {
        _kind = kind;
        _radius = radius;
        _halfExtents = halfExtents;
    }

    public Kind Type => _kind;

    public float Radius
    {
        get
        {
            Debug.Assert(_kind == Kind.Circle, "Radius accessed on non-circle shape"); return _radius;
        }
    }

    public Vec2 HalfExtents
    {
        get
        {
            Debug.Assert(_kind == Kind.Box, "HalfExtents accessed on non-box shape"); return _halfExtents;
        }
    }

    public static Shape Circle(float radius)   => new(Kind.Circle, radius, default);
    public static Shape Box(Vec2 halfExtents)  => new(Kind.Box, 0f, halfExtents);

    public AABB GetAABB(in Transform2D t) => _kind switch
    {
        Kind.Circle => AABB.FromCenterRadius(t.Origin, _radius),
        Kind.Box    => AABB.FromBox(t.Origin, t.Rotation, _halfExtents),
        _ => throw new UnreachableException(),
    };

    public float Circumradius => _kind switch
    {
        Kind.Circle => _radius,
        Kind.Box    => MathF.Sqrt(
            _halfExtents.X * _halfExtents.X +
            _halfExtents.Y * _halfExtents.Y),
        _ => throw new UnreachableException(),
    };
}