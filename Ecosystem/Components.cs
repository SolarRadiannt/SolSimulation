using fennecs;

namespace SolSimulation.Ecosystem;

public struct Stomach(float maxSatiety)
{
    public float MaxSatiety = maxSatiety;
    public float Satiety = maxSatiety;

    public readonly float HungerRatio => 1 - (Satiety / MaxSatiety); 
}
