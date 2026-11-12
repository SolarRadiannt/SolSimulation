namespace SolSimulation.Main.Systems;
using fennecs;
using SolTK;

public struct Prey;


public class PreySystem : IService
{
    private static readonly World _world = ECS.World;
    public static readonly EntityTemplate PreyTemplate = _world.Template();
}