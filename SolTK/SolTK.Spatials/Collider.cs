namespace SolTK.Spatials;
using Godot;

public struct Collider
{
    public readonly Shape Shape;
    public uint? Mask = null;

    private Collider(Shape shape) { Shape = shape; }
    
    public static Collider Circle(float radius)    => new(Shape.Circle(radius));
    public static Collider Box(Vec2 halfExtents)   => new(Shape.Box(halfExtents));
}