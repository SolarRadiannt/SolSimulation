Repo for my various simulation project.

Runs in Godot but the architecture relied heavily on Entities using fennecs and is Entity-First as most things will be Entities instead of Nodes.
EcoSystem branch is at: [link here]

Available Components currently:
- Motion - For moving the entities Transform2D.
- Collider - To be queryable by the SpatialSystem's spatial grid.
- MoveTo - Will make Motion.MoveDirection to go towards MoveTo.Goal.
- UtilityAgent - Holds list of actions (UtilityAgent.Actions) to iterate on, check its score and apply the action to UtilityAgent.CurrentAction and Enter the state.
- RenderDesc - Description of how to render the entity. It takes in what shape to render and the colo, then i'll be rendered on RenderSystem via Godot's RenderingServer.

Available Tags currently:
- AgentDisabled - Excludes the entity from UtilityAISystem Evaluation even if it has the UtilityAgent component.
