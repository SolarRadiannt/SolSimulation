namespace SolSimulation.Ecosystem.Actions;

using fennecs;
using SolSimulation.Main.Systems;

public struct Eating;
public struct WantToEat;

public class EatAction : UtilityAction
{
    public override bool CanEnter(in EntityRef entityRef) =>
        entityRef.Has<Stomach>() && entityRef.Has<WantToEat>(Match.Any);
    
    public override float Score(in EntityRef entityRef) => 0.8f;
    public override void Entered(in EntityRef entityRef)
    {
        entityRef.Add<Eating>();
    }

    public override void Exited(in EntityRef entityRef)
    {
        entityRef.Remove<Eating>();
    }
}