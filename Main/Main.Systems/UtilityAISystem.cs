namespace SolSimulation.Main.Systems;

using System.Collections.Generic;
using fennecs;
using SolTK;



public struct AgentDisabled;

public readonly record struct AgentChangedActionEvent(
    BaseUtilityAction? LastAction,
    BaseUtilityAction NewAction
);

public struct UtilityAgent(IReadOnlyList<BaseUtilityAction> actions)
{
    public readonly IReadOnlyList<BaseUtilityAction> Actions = actions;
    public BaseUtilityAction? CurrentAction = null;
}

public abstract class BaseUtilityAction
{
    public abstract float Score(Entity entity);

    public abstract bool CanEnter(Entity entity);

    public abstract void Entered(Entity entity);
    public abstract void Exited(Entity entity);
}

public class UtilityAISystem : IService
{
    private static readonly World _world = ECS.World;
    private static readonly FilteredStream<UtilityAgent> _toEvaluate =
        _world.Stream<UtilityAgent>()
            .Not(Comp<AgentDisabled>.Plain)
            .Where(static (in UtilityAgent agent) => agent.Actions.Count > 0);
    private static void EvaluateAgents(in EntityRef entityRef, ref UtilityAgent agent)
    {
        BaseUtilityAction bestAction = null;
        float highestScore = float.MinValue;
        var entity = entityRef.Entity;

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
        Scheduler.RegisterPhysics(
            static _ => _toEvaluate.For(EvaluateAgents),
            STKPriority.Transformation
        );
    }
}