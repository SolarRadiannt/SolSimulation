namespace SolSimulation.Main.Systems;

using System.Collections.Generic;
using fennecs;
using SolTK;



public struct AgentDisabled;

public readonly record struct AgentChangedActionEvent(
    UtilityAction? OldAction,
    UtilityAction NewAction
);

public struct UtilityAgent(IReadOnlyList<UtilityAction> actions)
{
    public readonly IReadOnlyList<UtilityAction> Actions = actions;
    public UtilityAction? CurrentAction = null;
}

public abstract class UtilityAction
{
    public abstract float Score(in EntityRef entityRef);
    public abstract bool CanEnter(in EntityRef entityRef);
    
    public abstract void Entered(in EntityRef entityRef);
    public abstract void Exited(in EntityRef entityRef);
}

public class UtilityAISystem : IService
{
    private static readonly World _world = ECS.World;
    private static readonly FilteredStream<UtilityAgent> _toEvaluate =
        _world.Stream<UtilityAgent>()
            .Not(Comp<AgentDisabled>.Plain)
            .Where(static (in UtilityAgent agent) => agent.Actions.Count > 0);
    private static void EvaluateAgents(in EntityRef entity, ref UtilityAgent agent)
    {
        UtilityAction bestAction = null;
        float highestScore = float.MinValue;

        foreach (var action in agent.Actions)
        {
            if (!action.CanEnter(entity)) continue;
            float score = action.Score(entity);
            if (score > highestScore)
            {
                highestScore = score;
                bestAction = action;
            }
        }

        if (bestAction != null && bestAction != agent.CurrentAction)
        {
            var lastAction = agent.CurrentAction;
            lastAction?.Exited(entity);

            agent.CurrentAction = bestAction;
            agent.CurrentAction.Entered(entity);
            var ev = new AgentChangedActionEvent(
                lastAction,
                bestAction
            );
            EventBus.RaiseLocal(entity, ref ev);
        }
    }

    public void Init()
    {
        Scheduler.RegisterSimulation(
            static _ => _toEvaluate.For(EvaluateAgents),
            STKPriority.Transformation
        );
    }
}