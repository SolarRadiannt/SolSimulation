namespace SolTK.Spatials;
using Godot;

public struct Collider(Shape shape)
{
    public readonly Shape Shape = shape;
    public uint? Mask = null;
    
    public static Collider Circle(float radius)    => new(Shape.Circle(radius));
    public static Collider Box(Vec2 halfExtents)   => new(Shape.Box(halfExtents));
}