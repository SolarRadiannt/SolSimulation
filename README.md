# SolSimulation - Hybrid ECS - Godot Architecture Prototype

**Status:** In Progress (Architectural Prototype)

**Stack:** C#, Godot ([RenderingServer](https://docs.godotengine.org/en/stable/classes/class_renderingserver.html)), [fennecs](https://fennecs.net) (ECS)

## Overview
A simulation project exploring a **hybrid Entity-First architecture** where game logic lives entirely in [fennecs](https://fennecs.net) `World`, while Godot is used primarily as a rendering and tooling layer via [RenderingServer](https://docs.godotengine.org/en/stable/classes/class_renderingserver.html). The goal is to keep simulation systems decoupled from the engine's node hierarchy.

## Architecture
- **Simulation World:** fennecs `World` holds all entities, components, and tags.
- **Systems:** Motion, SpatialSystem (grid query), UtilityAISystem, RenderSystem.
- **Render Bridge:** `RenderDesc` components are consumed by a render system that issues draw calls through Godot's `RenderingServer`.
- **Engine Role:** Godot handles lifecycle, windowing, input, and low-level rendering; it does not own simulation logic.

## Current Components
| Component | Purpose |
|-----------|---------|
| `Motion` | Moves entity `Transform2D` |
| `Collider` | Queryable by `SpatialSystem` spatial grid |
| `MoveTo` | Drives `Motion` toward a goal |
| `UtilityAgent` | Holds Utility AI Actions to be evaluated |
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

## Next Steps
- Refactor input handling into an ECS system.
- Add a debug overlay using ImGui (already included in `addons/`).

## Branch
[Ecosystem](https://foo.com)
