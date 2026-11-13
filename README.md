# SolSimulation - Hybrid ECS - Godot Architecture Prototype

**Status:** In Progress (Architectural Prototype)

**Stack:** C#, Godot ([RenderingServer](https://docs.godotengine.org/en/stable/classes/class_renderingserver.html)), [fennecs](https://fennecs.net) (ECS)

## Overview
A simulation project exploring a **hybrid Entity-First architecture** where game logic lives entirely in [fennecs](https://fennecs.net) `World`, while Godot is used primarily as a rendering and tooling layer via [RenderingServer](https://docs.godotengine.org/en/stable/classes/class_renderingserver.html). The goal is to keep simulation systems decoupled from the engine's node hierarchy.

## Architecture
- **Simulation World:** fennecs `World` holds all entities and Query their components and tags.
- **Systems:** Motion, SpatialSystem (grid query), UtilityAISystem, RenderSystem.
- **Render Bridge:** `RenderDesc` components are consumed by a render system that issues draw calls through Godot's `RenderingServer`.
- **Engine Role:** Godot handles lifecycle, windowing, input, and low-level rendering; it does not own simulation logic.
- **UtilityAI:** a scoring-based AI framework where actions are evaluated by weighted considerations.

## Current Components
| Component | Purpose |
|-----------|---------|
| `Motion` | Moves entity `Transform2D` |
| `Collider` | Queryable by `SpatialSystem` spatial grid |
| `MoveTo` | Drives `Motion` toward a goal |
| `UtilityAgent` | Holds UtilityAI Actions to be evaluated |
| `RenderDesc` | Shape/color for `RenderingServer` draw calls |

**Tags:** `AgentDisabled` (excluded from UtilityAISystem evaluation)

## How to Run
1. Prerequisites: [Godot Installation](https://godotengine.org/download/windows/) Choose [Godot Engine – .NET].
2. Clone the repo.
3. Open in Godot and run the main scene.

Currently, the project includes a drawing test system: hold the mouse button to spawn an entity with RenderDesc and Transform2D at the mouse position. **Press Space to toggle drawing**.

## What Works Now
- [x] Core fennecs world initializes.
- [x] RenderSystem draws entities via `RenderingServer` using `RenderDesc`.
- [x] Spatial grid queries functional.
- [x] UtilityAI evaluation loop.
- [x] Drawing test system.
- [x] ECS Inspector using ImGui with the capability to view entities Components/Tags and edit Components. 

## Next Steps
- Narrow phase for SpatialSystem, currently they only have QueryBroadAABB.
- Raycast method in SpatialSystem.
- Add Component/Tags capability for the ECS Inspector.
- Sprite render support for RenderDesc.

## Branch
[Ecosystem](https://foo.com)
