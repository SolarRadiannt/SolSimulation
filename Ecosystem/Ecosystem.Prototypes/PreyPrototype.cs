namespace SolSimulation.Ecosystem.Prototypes;
using SolSimulation.Main.Systems;
using fennecs;
using Godot;
using SolTK;
using SolTK.Spatials;
using System;


public struct Prey;


public static class PreyPrototype
{
    private static readonly World _world = ECS.World;
    private static readonly Shape _shape = Shape.Circle(10);

    private static readonly float _baseSpeed = 400;
    private static readonly float _baseTurnSpeed = 45;

    public static readonly EntityTemplate<Transform2D> Template =
        _world.Template()
            .Add<Prey>()
            .Add<Name>("Prey")

            .Add(new Stomach(100))

            .Add(new Collider(_shape))
            .Add(new RenderDesc(_shape, new("#0000ff")))
            .Add(new Motion(_baseSpeed))
            .Add(new MoveAutoRotate(_baseTurnSpeed))
            .Needs<Transform2D>();
}