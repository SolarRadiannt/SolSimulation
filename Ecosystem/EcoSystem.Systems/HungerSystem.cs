namespace SolSimulation.Ecosystem.Systems;

using fennecs;
using Godot;
using SolTK;

public struct Starving;

public class HungerSystem : IService
{
    private static readonly World _world = ECS.World;

    private static readonly Stream<Stomach> _starveables = 
        _world.Stream<Stomach>();
    
    private static void CreatureStarvingCheck(in EntityRef entityRef, ref Stomach stomach)
    {
        if (stomach.Satiety > 0)
        {
            if (entityRef.Has<Starving>())
                entityRef.Remove<Starving>();
        }
        else
        {
            if (!entityRef.Has<Starving>())
                entityRef.Add<Starving>();
        }
    }

    private static readonly FilteredStream<Stomach> _hungerables =
        _world.Stream<Stomach>()
            .Not(Comp<Starving>.Plain);
    private static void HungerCreature(float dt, in EntityRef entityRef, ref Stomach stomach)
    {
        float hungerAmount = stomach.HungerRate * dt;
        stomach.Satiety = Mathf.Max(
            0,
            stomach.Satiety - hungerAmount
        );
    }

    public void Init()
    {
        Scheduler.Register(static _ =>
            _starveables.For(CreatureStarvingCheck),
            STKPriority.Interception
        );

        Scheduler.Register(static dt => 
            _hungerables.For((float)dt, HungerCreature),
            STKPriority.Applying
        );
    }
}