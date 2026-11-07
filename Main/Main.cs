namespace SolSimulation.Main;

using System;
using Godot;
using SolTK;

public partial class Main : Node2D
{
	public override void _Ready()
	{
		IoCManager.OnError = (name, ex) => GD.PushError($"[IoCManager] {name}: {ex.Message}");
		Scheduler.OnError  = (name, ex) => GD.PushError($"[Scheduler] {name}: {ex.Message}");

		IoCManager.InitAll();
	}

	public override void _Process(double delta)
	{
		Scheduler.ProcessAll(delta);
	}

	public override void _PhysicsProcess(double delta)
	{
		Scheduler.ProcessAllPhysics(delta);
	}
}
