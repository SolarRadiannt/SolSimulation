using System.Collections.Generic;
using fennecs;
using SolTK;

namespace SolSimulation.Main;

public struct AgentDisabled;

public readonly struct UtilityAgent
{
    public readonly IReadOnlyList<BaseUtilityAction> Actions;
}

public abstract class BaseUtilityAction : ISystem
{
    public abstract float Score(Entity entity);

	public abstract bool CanEnter(Entity entity);

	public abstract void Entered(Entity entity);
	public abstract void Exited(Entity entity);
    
    public abstract void Init();
}

public class UtilityAI : ISystem
{

    public void Init()
    {
        
    }
}