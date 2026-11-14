namespace SolSimulation.Main;

using System;
using Godot;
using SolTK;

public partial class Main : Node2D
{
	private static Main _instance;
	public static Main Instance => _instance;

	public override void _Ready()
	{
		_instance = this;
		IoCManager.OnError = (name, ex) => GD.PushError($"[IoCManager] {name}: {ex.Message}");
		Scheduler.OnError  = (name, ex) => GD.PushError($"[Scheduler] {name}: {ex.Message}");

		IoCManager.InitAll();
	}

	public override void _Process(double delta)
	{
		CentralClock.TickReal();
		Scheduler.ProcessAll(delta);
	}

	public override void _PhysicsProcess(double delta)
	{
		CentralClock.TickSimulation(TimeSpan.FromSeconds(delta));
		Scheduler.ProcessAllSimulation(delta);
	}
}
