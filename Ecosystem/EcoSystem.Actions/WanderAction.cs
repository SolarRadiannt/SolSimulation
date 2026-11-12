namespace SolSimulation.Ecosystem.Actions;

using fennecs;
using SolSimulation.Main.Systems;

public struct Wandering;

public class WanderAction : UtilityAction
{
    public override bool CanEnter(in EntityRef entityRef) => true;
    public override float Score(in EntityRef entityRef) => 0.65f;

    public override void Entered(in EntityRef entityRef)
    {
        entityRef.Add<Wandering>();
    }
    public override void Exited(in EntityRef entityRef)
    {
        entityRef.Remove<Wandering>();
    }
}