namespace SolSimulation.Ecosystem.Actions;

using fennecs;
using SolSimulation.Main.Systems;

public struct Hunting;

public class HuntAction : UtilityAction
{
    public override bool CanEnter(in EntityRef entityRef) => entityRef.Has<Stomach>();

    public override float Score(in EntityRef entityRef) =>
        entityRef.Ref<Stomach>().HungerRatio;

    public override void Entered(in EntityRef entityRef)
    {
        entityRef.Add<Hunting>();
    }

    public override void Exited(in EntityRef entityRef)
    {
        entityRef.Remove<Hunting>();
    }
}