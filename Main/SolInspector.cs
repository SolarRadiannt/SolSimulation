namespace SolSimulation.Main;

using Godot;
using fennecs;
using ImGuiGodot;
using ImGuiNET;
using SolTK;
using SolTK.Components;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using SolTK.Extensions;
using NumVec4 = System.Numerics.Vector4;
using NumVec2 = System.Numerics.Vector2;
using SysVec2 = System.Numerics.Vector2;
using SysVec3 = System.Numerics.Vector3;
using SysVec4 = System.Numerics.Vector4;

// ========================================================================
// DATA STRUCTS & ATTRIBUTES
// ========================================================================

public readonly struct EntSnapshot
{
	public Entity Entity { get; init; }
	public IReadOnlyList<EntComponentSnapshot> Components { get; init; }
	public IReadOnlyList<EntRelationshipSnapshot> Relationships { get; init; }
	public IReadOnlyList<EntTagSnapshot> Tags { get; init; }
}

public readonly record struct EntComponentSnapshot(Type Type, string DisplayName, NumVec4 Color, object Value);
public readonly record struct EntTagSnapshot(Type Type, string DisplayName, NumVec4 Color);
public readonly record struct EntRelationshipSnapshot(string Label, Entity Target);

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class InspectorColorAttribute : Attribute
{
	public NumVec4 Color { get; }
	public InspectorColorAttribute(float r, float g, float b, float a = 1.0f) => Color = new NumVec4(r, g, b, a);
	public InspectorColorAttribute(InspectColor preset) => Color = InspectColors.Get(preset);
}

public readonly struct ComponentTypeCache
{
	public readonly Type Type;
	public readonly bool IsTag;
	public readonly string DisplayName;
	public readonly FieldInfo[] Fields;
	public readonly PropertyInfo[] Properties;
	public readonly NumVec4 Color;

	public ComponentTypeCache(Type type, bool isTag, string displayName, FieldInfo[] fields, PropertyInfo[] properties, NumVec4 color)
	{
		Type = type;
		IsTag = isTag;
		DisplayName = displayName;
		Fields = fields;
		Properties = properties;
		Color = color;
	}
}

// ========================================================================
// MAIN INSPECTOR CLASS
// ========================================================================

public class SolInspector : IService, IProcessable
{
	#region Constants & Static Fields
	private const string TITLE = "Sol ECS Entities";
	private const int MAX_RECURSION_DEPTH = 7;
	private const int MAX_RELATIONSHIP_DEPTH = 5;
	private const float HORIZONTAL_TAGS_SPACE = 16f;

	private static readonly NumVec4 DefaultColor = new(0.40f, 0.40f, 0.50f, 1.0f);
	private static readonly NumVec4 TitleColor = new(1f, 1f, 1f, 1f);
	private static readonly NumVec4 Amber = new(1.0f, 0.75f, 0.0f, 1.0f);
	private static readonly InspectorColorAttribute GodotColorAttr = new(InspectColor.Violet);

	private static readonly World world = ECS.World;
	private static readonly Stream<MarkDespawn> _despawningEntities = world.Stream<MarkDespawn>();

	private static readonly Dictionary<Type, MethodInfo> _setMethods = [];
	private static readonly MethodInfo _setValueMethod = typeof(SolInspector).GetMethod(nameof(SetValue), BindingFlags.NonPublic | BindingFlags.Static);
	#endregion

	#region Instance State
	private readonly Dictionary<Type, ComponentTypeCache> _memberCache = [];
	private string _searchFilter = ""; 

	// Multi-window state
	private class InspectorWindowState(Entity entity)
	{
		public readonly Entity Entity = entity;
		public Type EditingComponent = null;
		public bool ShowComponentWindow = false;
		public NumVec2 WindowPos;
		public NumVec2 WindowSize;
		public readonly Dictionary<Type, object> PendingEdits = [];
	}

	private readonly Dictionary<Entity, InspectorWindowState> _openInspectors = [];
	#endregion

	#region Public API
	/// <summary>
	/// Pops open (or brings to front) an Inspector window for the specified entity.
	/// Safe to call from anywhere in your game code.
	/// </summary>
	public void OpenInspector(Entity entity)
	{
		if (!entity.Alive) return;
		
		if (!_openInspectors.ContainsKey(entity))
		{
			_openInspectors[entity] = new InspectorWindowState(entity);
		}
	}
	#endregion

	#region Core Data Extraction & Caching
	private static string GetFriendlyTypeName(Type type)
	{
		if (!type.IsGenericType) return type.Name;
		int backtickIndex = type.Name.IndexOf('`');
		string cleanName = backtickIndex > 0 ? type.Name.Substring(0, backtickIndex) : type.Name;
		string genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
		return $"{cleanName}<{genericArgs}>";
	}

	private ComponentTypeCache GetOrCacheType(Type type)
	{
		if (_memberCache.TryGetValue(type, out var members)) return members;

		var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
		var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
		bool isTag = fields.Length == 0 && properties.Length == 0;
		string displayName = GetFriendlyTypeName(type);

		NumVec4 color = DefaultColor;
		var colorAttr = type.GetCustomAttribute<InspectorColorAttribute>();

		if (typeof(GodotObject).IsAssignableFrom(type))
			colorAttr = GodotColorAttr;
		else if (colorAttr == null && type.IsGenericType)
		{
			colorAttr = type.GetGenericTypeDefinition().GetCustomAttribute<InspectorColorAttribute>();
			if (colorAttr == null)
			{
				foreach (var genericArg in type.GetGenericArguments())
				{
					colorAttr = genericArg.GetCustomAttribute<InspectorColorAttribute>();
					if (colorAttr != null) break;
				}
			}
		}

		if (colorAttr != null) color = colorAttr.Color;

		members = new ComponentTypeCache(type, isTag, displayName, fields, properties, color);
		return _memberCache[type] = members;
	}

	private static void ExtractRelationships(in ComponentTypeCache cache, object data, List<EntRelationshipSnapshot> relationships)
	{
		foreach (var f in cache.Fields)
			if (f.FieldType == typeof(Entity) && f.GetValue(data) is Entity t)
				relationships.Add(new($"{cache.DisplayName}.{f.Name}", t));

		foreach (var p in cache.Properties)
			if (p.PropertyType == typeof(Entity) && p.CanRead && p.GetIndexParameters().Length == 0)
				try { if (p.GetValue(data) is Entity t) relationships.Add(new($"{cache.DisplayName}.{p.Name}", t)); }
				catch { /* property threw — skip */ }
	}

	private static void SetValue<T>(Entity e, object boxed)
	{
		ref var comp = ref e.Ref<T>();
		comp = (T)boxed;
	}

	private static void WriteBack(Entity e, Type t, object boxed)
	{
		try
		{
			if (!_setMethods.TryGetValue(t, out var m))
			{
				m = _setValueMethod?.MakeGenericMethod(t);
				if (m == null) return;
				_setMethods[t] = m;
			}
			m.Invoke(null, [e, boxed]);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[SolInspector] Failed to write back {t.Name}: {ex.InnerException?.Message ?? ex.Message}");
		}
	}
	#endregion

	#region UI Rendering - Main Flow
	private void DrawMainWindow()
	{
		ImGui.SetNextWindowSize(new NumVec2(320, 500), ImGuiCond.FirstUseEver);
		if (ImGui.Begin(TITLE, ImGuiWindowFlags.NoCollapse))
		{
			try
			{
				HandleSearch("Search Entities:", 0, ref _searchFilter);
				ImGui.Separator();

				ImGui.BeginChild("EntityListScroll");
				foreach (Entity entity in world.All)
				{
					if (!entity.Alive) continue;
					if (IsFiltered(entity.Name(), _searchFilter)) continue;

					string label = $"[{entity.ToRaw()}] {entity.Name()}";
					bool isSelected = _openInspectors.ContainsKey(entity);

					if (ImGui.Selectable(label, isSelected))
					{
						OpenInspector(entity);
					}
				}
				ImGui.EndChild();
			}
			finally
			{
				ImGui.End();
			}
		}
	}

	private void DrawEntityWindows()
	{
		// Snapshot keys so adding/removing windows during iteration is safe
		var entities = _openInspectors.Keys.ToList();
		
		foreach (var entity in entities)
		{
			if (!entity.Alive)
			{
				_openInspectors.Remove(entity);
				continue;
			}

			DrawEntityWindow(_openInspectors[entity]);
		}
	}

	private void DrawEntityWindow(InspectorWindowState state)
	{
		string windowTitle = $"Inspector - [{state.Entity.ToRaw()}] {state.Entity.Name()}###EntityWindow_{state.Entity.GetHashCode()}";
		ImGui.SetNextWindowSize(new NumVec2(350, 500), ImGuiCond.FirstUseEver);
		
		bool open = true;
		if (ImGui.Begin(windowTitle, ref open))
		{
			try
			{
				state.WindowPos = ImGui.GetWindowPos();
				state.WindowSize = ImGui.GetWindowSize();

				if (state.Entity.Has<MarkDespawn>())
					ImGui.TextColored(Amber, "(Marked for despawn)");

				var snapshot = GetSnapshot(state.Entity);
				ShowRelationships(state.Entity, [], 0, snapshot.Relationships);
				
				ImGui.Separator();
				ImGui.TextColored(TitleColor, "Components (Right-click to edit)");
				ImGui.Spacing();

				foreach (var comp in snapshot.Components)
				{
					ImGui.PushStyleColor(ImGuiCol.Header, comp.Color);
					ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new NumVec4(comp.Color.X * 1.2f, comp.Color.Y * 1.2f, comp.Color.Z * 1.2f, 1f));
					ImGui.PushStyleColor(ImGuiCol.HeaderActive, new NumVec4(comp.Color.X * 1.4f, comp.Color.Y * 1.4f, comp.Color.Z * 1.4f, 1f));

					bool isOpen = ImGui.CollapsingHeader(comp.DisplayName, ImGuiTreeNodeFlags.None);
					ImGui.PopStyleColor(3);

					// Right-click opens the edit panel
					if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
					{
						state.EditingComponent = comp.Type;
						state.ShowComponentWindow = true;
					}

					if (isOpen)
					{
						ImGui.Indent();
						DrawComponentReadOnly(comp);
						ImGui.Unindent();
					}
				}

				ShowTags(snapshot.Tags);
			}
			finally
			{
				ImGui.End();
			}
		}

		if (!open)
		{
			_openInspectors.Remove(state.Entity);
		}
	}

	private void DrawComponentEditWindows()
	{
		var entities = _openInspectors.Keys.ToList();
		
		foreach (var entity in entities)
		{
			if (!entity.Alive) continue;
			
			var state = _openInspectors[entity];
			if (state.ShowComponentWindow && state.EditingComponent != null)
			{
				DrawComponentEditWindow(state);
			}
		}
	}

	private void DrawComponentEditWindow(InspectorWindowState state)
	{
		var snapshot = GetSnapshot(state.Entity);
		var comp = snapshot.Components.FirstOrDefault(c => c.Type == state.EditingComponent);

		if (comp.Type == null)
		{
			state.ShowComponentWindow = false;
			state.EditingComponent = null;
			return;
		}

		string windowTitle = $"Editing - {comp.DisplayName}###ComponentEditWindow_{state.Entity.GetHashCode()}";
		
		// Dock to the right of its specific parent Entity Window
		NumVec2 spawnPos = new NumVec2(state.WindowPos.X + state.WindowSize.X, state.WindowPos.Y);
		ImGui.SetNextWindowPos(spawnPos, ImGuiCond.Always);

		// Lock width to 400px, allow height to shrink-wrap
		ImGui.SetNextWindowSizeConstraints(new NumVec2(400, 0), new NumVec2(400, ImGui.GetIO().DisplaySize.Y - 100));

		var flags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize;

		bool open = true;
		if (ImGui.Begin(windowTitle, ref open, flags))
		{
			try
			{
				ImGui.PushTextWrapPos(390f);
				ImGui.PushItemWidth(180f);

				ImGui.PushStyleColor(ImGuiCol.Text, TitleColor);
				ImGui.Text(comp.DisplayName);
				ImGui.PopStyleColor();
				ImGui.Separator();

				DrawComponentEditable(state, comp);
				
				ImGui.PopItemWidth();
				ImGui.PopTextWrapPos();
			}
			finally
			{
				ImGui.End();
			}
		}

		if (!open)
		{
			state.EditingComponent = null;
			state.ShowComponentWindow = false;
			state.PendingEdits.Clear();
		}
	}
	#endregion

	#region UI Rendering - Sections
	private void DrawComponentReadOnly(EntComponentSnapshot comp)
	{
		var cache = GetOrCacheType(comp.Type);

		foreach (var field in cache.Fields)
			DisplayValue(field.Name, field.GetValue(comp.Value));

		foreach (var prop in cache.Properties)
		{
			if (prop.GetIndexParameters().Length == 0 && prop.CanRead)
				DisplayValue(prop.Name, prop.GetValue(comp.Value));
		}
	}

	private void DrawComponentEditable(InspectorWindowState state, EntComponentSnapshot comp)
	{
		var cache = GetOrCacheType(comp.Type);
		bool isStruct = comp.Type.IsValueType;

		object boxed = state.PendingEdits.TryGetValue(comp.Type, out var pending) ? pending : comp.Value;
		bool changed = false;
		bool committed = false;

		foreach (var f in cache.Fields)
		{
			var child = f.GetValue(boxed);
			var (nv, ch, deactField) = DrawValueEditable(f.Name, child, f.FieldType, 0);
			if (ch) 
			{ 
				f.SetValue(boxed, nv); 
				changed = true; 
				state.PendingEdits[comp.Type] = boxed; 
			}
			if (deactField) committed = true;
		}

		foreach (var p in cache.Properties)
		{
			if (p.GetIndexParameters().Length != 0 || !p.CanRead) continue;
			bool canWrite = p.SetMethod is { IsPublic: true };

			try
			{
				var child = p.GetValue(boxed);
				var (nv, ch, deactProp) = canWrite
					? DrawValueEditable(p.Name, child, p.PropertyType, 0)
					: DrawValueEditable(p.Name, child, p.PropertyType, 0, allowEdit: false);

				if (canWrite && ch) 
				{ 
					p.SetValue(boxed, nv); 
					changed = true; 
					state.PendingEdits[comp.Type] = boxed; 
				}
				if (deactProp) committed = true;
			}
			catch { ImGui.Text($"{p.Name}: [Error]"); }
		}

		// Only structs need pending tracking — class mutations already live on the entity.
		if (!isStruct) return;

		// Auto-Commit: If the user released focus (mouse up / enter), write it back to ECS
		if (committed)
		{
			if (state.PendingEdits.TryGetValue(comp.Type, out var finalBoxed))
			{
				WriteBack(state.Entity, comp.Type, finalBoxed);
				state.PendingEdits.Remove(comp.Type);
			}
		}
	}

	private static void ShowTags(IReadOnlyList<EntTagSnapshot> tags)
	{
		if (tags.Count == 0) return;

		ImGui.TextColored(TitleColor, "Tags:");
		
		float availableWidth = ImGui.GetContentRegionAvail().X;
		float currentWidth = 0.0f;
		bool firstInRow = true;

		foreach (var tag in tags)
			DisplayTag(tag.DisplayName, tag.Color, ref firstInRow, ref currentWidth, availableWidth);
			
		ImGui.Spacing();
	}

	private void ShowRelationships(Entity entity, HashSet<Entity> visited, int depth, IReadOnlyList<EntRelationshipSnapshot> relationships)
	{
		if (depth > MAX_RELATIONSHIP_DEPTH || relationships.Count == 0) return;

		ImGui.TextColored(TitleColor, "Relationships:");

		foreach (var rel in relationships)
		{
			string nodeLabel = $"{rel.Label} -> [{rel.Target.ToRaw()}] {rel.Target.Name()}";
			if (visited.Contains(rel.Target))
			{
				ImGui.TextColored(Amber, $"{nodeLabel} (Circular)");
				continue;
			}

			ImGui.PushID(nodeLabel);
			ImGui.AlignTextToFramePadding();
			ImGui.Text(nodeLabel);
			ImGui.SameLine();
			if (ImGui.SmallButton("Select")) 
			{ 
				OpenInspector(rel.Target); 
			}
			ImGui.PopID();
		}
		
		ImGui.Spacing();
	}
	#endregion

	#region UI Rendering - Editable Values
	private static bool IsNonEditable(object value, Type t)
	{
		if (value is null) return true;
		if (t == typeof(Entity)) return true;
		if (typeof(GodotObject).IsAssignableFrom(t)) return true;
		return false;
	}

	private static unsafe bool DrawEditable(string name, Type t, ref object v, out bool deactivated)
	{
		deactivated = false;
		bool changed = false;

		switch (v)
		{
			case bool b:
			{
				if (ImGui.Checkbox(name, ref b)) { v = b; changed = true; }
				break;
			}
			case byte by:
			{
				int tmp = by;
				if (ImGui.DragInt(name, ref tmp, 1, 0, 255)) { v = (byte)tmp; changed = true; }
				break;
			}
			case sbyte sb:
			{
				int tmp = sb;
				if (ImGui.DragInt(name, ref tmp, 1, -128, 127)) { v = (sbyte)tmp; changed = true; }
				break;
			}
			case short sh:
			{
				int tmp = sh;
				if (ImGui.DragInt(name, ref tmp, 1, -32768, 32767)) { v = (short)tmp; changed = true; }
				break;
			}
			case ushort us:
			{
				int tmp = us;
				if (ImGui.DragInt(name, ref tmp, 1, 0, 65535)) { v = (ushort)tmp; changed = true; }
				break;
			}
			case int i:
			{
				if (ImGui.DragInt(name, ref i)) { v = i; changed = true; }
				break;
			}
			case uint ui:
			{
				uint tmp = ui;
				if (ImGui.DragScalar(name, ImGuiDataType.U32, (nint)(&tmp))) { v = tmp; changed = true; }
				break;
			}
			case long l:
			{
				long tmp = l;
				if (ImGui.DragScalar(name, ImGuiDataType.S64, (nint)(&tmp))) { v = tmp; changed = true; }
				break;
			}
			case ulong ul:
			{
				ulong tmp = ul;
				if (ImGui.DragScalar(name, ImGuiDataType.U64, (nint)(&tmp))) { v = tmp; changed = true; }
				break;
			}
			case float f:
			{
				if (ImGui.DragFloat(name, ref f, 0.01f)) { v = f; changed = true; }
				break;
			}
			case double d:
			{
				double tmp = d;
				if (ImGui.DragScalar(name, ImGuiDataType.Double, (nint)(&tmp), 0.01f)) { v = tmp; changed = true; }
				break;
			}
			case Vector2 gv2:
			{
				var sv = new SysVec2(gv2.X, gv2.Y);
				if (ImGui.DragFloat2(name, ref sv, 0.01f)) { v = new Vector2(sv.X, sv.Y); changed = true; }
				break;
			}
			case Vector3 gv3:
			{
				var sv = new SysVec3(gv3.X, gv3.Y, gv3.Z);
				if (ImGui.DragFloat3(name, ref sv, 0.01f)) { v = new Vector3(sv.X, sv.Y, sv.Z); changed = true; }
				break;
			}
			case Vector4 gv4:
			{
				var sv = new SysVec4(gv4.X, gv4.Y, gv4.Z, gv4.W);
				if (ImGui.DragFloat4(name, ref sv, 0.01f)) { v = new Vector4(sv.X, sv.Y, sv.Z, sv.W); changed = true; }
				break;
			}
			case SysVec2 nv2:
			{
				if (ImGui.DragFloat2(name, ref nv2, 0.01f)) { v = nv2; changed = true; }
				break;
			}
			case SysVec3 nv3:
			{
				if (ImGui.DragFloat3(name, ref nv3, 0.01f)) { v = nv3; changed = true; }
				break;
			}
			case SysVec4 nv4:
			{
				if (ImGui.DragFloat4(name, ref nv4, 0.01f)) { v = nv4; changed = true; }
				break;
			}
			case string s:
			{
				var tmp = s;
				if (ImGui.InputText(name, ref tmp, 256)) { v = tmp; changed = true; }
				break;
			}
			default:
			{
				if (t.IsEnum)
				{
					var names = Enum.GetNames(t);
					var values = Enum.GetValues(t).Cast<object>().ToArray();
					int cur = Array.IndexOf(values, v);
					if (cur < 0) cur = 0;
					if (ImGui.Combo(name, ref cur, names, names.Length)) { v = values[cur]; changed = true; }
				}
				break;
			}
		}

		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			deactivated = true;
		}

		return changed;
	}

	private (object value, bool changed, bool deactivated) DrawValueEditable(string name, object value, Type declType, int depth, bool allowEdit = true)
	{
		ImGui.PushID(name);
		try
		{
			if (value is null) return (null, false, false);
			if (depth > MAX_RECURSION_DEPTH) return (value, false, false);

			if (IsNonEditable(value, declType)) return (value, false, false);
			if (IsEditableLeaf(declType) && !allowEdit) return (value, false, false);

			if (allowEdit)
			{
				bool ch = DrawEditable(name, declType, ref value, out bool deact);
				if (ch || deact)
					return (value, ch, deact);

				if (IsEditableLeaf(declType))
					return (value, false, false);
			}

			if (!ImGui.TreeNode($"{name} ({GetFriendlyTypeName(declType)})"))
				return (value, false, false);

			bool changed = false;
			bool deactivated = false;
			object boxed = value;

			var cache = GetOrCacheType(declType);
			foreach (var f in cache.Fields)
			{
				var child = f.GetValue(boxed);
				var (nv, ch, deactField) = DrawValueEditable(f.Name, child, f.FieldType, depth + 1);
				if (ch) { f.SetValue(boxed, nv); changed = true; }
				if (deactField) deactivated = true;
			}

			foreach (var p in cache.Properties)
			{
				if (p.GetIndexParameters().Length != 0 || !p.CanRead) continue;
				try
				{
					var child = p.GetValue(boxed);
					var (nv, ch, deactProp) = DrawValueEditable(p.Name, child, p.PropertyType, depth + 1,
						allowEdit: p.SetMethod is { IsPublic: true });
					if (ch) { p.SetValue(boxed, nv); changed = true; }
					if (deactProp) deactivated = true;
				}
				catch { /* Skip broken properties silently in edit mode */ }
			}

			ImGui.TreePop();
			return (boxed, changed, deactivated);
		}
		finally
		{
			ImGui.PopID();
		}
	}

	private static bool IsEditableLeaf(Type t) =>
		t.IsPrimitive || t.IsEnum || t == typeof(string) ||
		t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4) ||
		t == typeof(SysVec2) || t == typeof(SysVec3) || t == typeof(SysVec4);
	#endregion

	#region UI Rendering - Read-only Values
	private void DisplayValue(string name, object value, int depth = 0)
	{
		if (depth > MAX_RECURSION_DEPTH) { ImGui.Text($"{name}: [Max Depth Reached]"); return; }
		if (value == null) { ImGui.Text($"{name}: null"); return; }
		if (DisplayPrecise(name, value)) return;

		var type = value.GetType();
		if (type.IsPrimitive || type.IsEnum || type == typeof(string) ||
			type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4))
		{
			ImGui.Text($"{name}: {value}");
			return;
		}

		if (ImGui.TreeNode($"{name} ({type.Name})"))
		{
			var members = GetOrCacheType(type);
			foreach (var field in members.Fields)
				DisplayValue(field.Name, field.GetValue(value), depth + 1);

			foreach (var prop in members.Properties)
			{
				if (prop.GetIndexParameters().Length == 0 && prop.CanRead)
					try { DisplayValue(prop.Name, prop.GetValue(value), depth + 1); }
					catch { ImGui.Text($"{prop.Name}: [Error Reading]"); }
			}
			ImGui.TreePop();
		}
	}

	private static bool DisplayPrecise(string name, object value)
	{
		if (value is float f) { ImGui.Text($"{name}: {f:F2}"); return true; }
		if (value is double d) { ImGui.Text($"{name}: {d:F2}"); return true; }
		if (value is decimal dec) { ImGui.Text($"{name}: {dec:F2}"); return true; }
		return false;
	}
	#endregion

	#region UI Rendering - Tags & Helpers
	private static void DisplayTag(string name, NumVec4 color, ref bool firstInRow, ref float currentWidth, float availableWidth)
	{
		float itemWidth = ImGui.CalcTextSize(name).X + HORIZONTAL_TAGS_SPACE;
		if (!firstInRow && (currentWidth + itemWidth) > availableWidth)
		{
			currentWidth = 0.0f;
			firstInRow = true;
		}

		if (!firstInRow) ImGui.SameLine();
		DrawTagBadge(name, color);
		currentWidth += itemWidth + 4.0f;
		firstInRow = false;
	}

	private static void DrawTagBadge(string text, NumVec4 color)
	{
		var drawList = ImGui.GetWindowDrawList();
		var cursorPos = ImGui.GetCursorScreenPos();
		var textSize = ImGui.CalcTextSize(text);
		NumVec2 padding = new(8, 4);
		NumVec2 size = new(textSize.X + (padding.X * 2), textSize.Y + (padding.Y * 2));

		uint bgColor = ImGui.ColorConvertFloat4ToU32(new NumVec4(color.X * 0.4f, color.Y * 0.4f, color.Z * 0.4f, 0.5f));
		uint textColor = ImGui.ColorConvertFloat4ToU32(color);

		drawList.AddRectFilled(cursorPos, new NumVec2(cursorPos.X + size.X, cursorPos.Y + size.Y), bgColor, 4.0f);
		drawList.AddText(new NumVec2(cursorPos.X + padding.X, cursorPos.Y + padding.Y), textColor, text);
		ImGui.Dummy(size);
	}

	private static bool IsFiltered(string target, string filter)
		=> !string.IsNullOrWhiteSpace(filter) && !target.Contains(filter, StringComparison.OrdinalIgnoreCase);

	private static void HandleSearch(string title, int uniqueId, ref string filter)
	{
		if (ImGui.Button($"Clear##{uniqueId}")) filter = "";
		ImGui.SameLine();
		ImGui.Text(title);
		ImGui.SameLine();
		ImGui.SetNextItemWidth(160.0f);
		ImGui.InputText($"##{title}_{uniqueId}", ref filter, 50, ImGuiInputTextFlags.EscapeClearsAll);
	}
	#endregion

	#region Public API & Lifecycle
	public int Priority => SPriority.Lowest;

	public void Init()
	{
		Scheduler.Register(this);
		ImGuiGD.Connect(() =>
		{
			DrawMainWindow();
			DrawEntityWindows();
			DrawComponentEditWindows();
		});
	}

	public EntSnapshot GetSnapshot(Entity entity)
	{
		var components = new List<EntComponentSnapshot>();
		var tags = new List<EntTagSnapshot>();
		var relationships = new List<EntRelationshipSnapshot>();

		foreach (var comp in entity.Components)
		{
			var cache = GetOrCacheType(comp.Type);

			if (cache.IsTag)
			{
				tags.Add(new EntTagSnapshot(cache.Type, cache.DisplayName, cache.Color));
				continue;
			}

			var value = entity.Get(comp.Type);
			if (value is null) continue;

			components.Add(new EntComponentSnapshot(cache.Type, cache.DisplayName, cache.Color, value));
			ExtractRelationships(in cache, value, relationships);
		}

		return new EntSnapshot
		{
			Entity = entity,
			Components = components,
			Tags = tags,
			Relationships = relationships,
		};
	}

	public void Process(double _) => ClearEntitiesFromDicts();

	private void ClearEntitiesFromDicts() =>
		_despawningEntities.For((in EntityRef entityRef, ref MarkDespawn _) =>
		{
			var entity = entityRef.Entity;

			// Cleanup inspector windows if the entity despawns
			if (_openInspectors.ContainsKey(entity))
			{
				_openInspectors.Remove(entity);
			}
		});
	#endregion
}
