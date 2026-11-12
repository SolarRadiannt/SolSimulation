using fennecs;

namespace SolSimulation.Ecosystem;

public struct Stomach(float maxSatiety, float hungerRate = 2)
{
    public float MaxSatiety = maxSatiety;
    public float Satiety = maxSatiety;
    public float HungerRate = hungerRate;

    public readonly float HungerRatio => 1 - (Satiety / MaxSatiety); 
}
