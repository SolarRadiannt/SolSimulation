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

public class SolInspector : ISystem, IProcessable
{
	#region Constants & Static Fields
	private const string TITLE = "Sol ECS Inspector";
	private const int MAX_RECURSION_DEPTH = 7;
	private const int MAX_RELATIONSHIP_DEPTH = 5;
	private const float HORIZONTAL_TAGS_SPACE = 16f;

	private static readonly NumVec4 DefaultColor = new(0.40f, 0.40f, 0.50f, 1.0f);
	private static readonly NumVec4 Amber = new(1.0f, 0.75f, 0.0f, 1.0f); 
	private static readonly InspectorColorAttribute GodotColorAttr = new(InspectColor.Violet);

	private static readonly World world = ECS.World;
	private static readonly Stream<MarkDespawn> _despawningEntities = world.Stream<MarkDespawn>();
	#endregion

	#region Instance State
	private readonly Dictionary<Type, ComponentTypeCache> _memberCache = [];
	private string _searchFilter = ""; // Cannot be readonly because ImGui mutates it via ref

	private readonly Dictionary<Entity, string> _entitiesComponentFilter = [];
	private readonly Dictionary<Entity, string> _entitiesTagsFilter = [];
	private readonly Dictionary<Entity, string> _entitiesRelationshipsFilter = [];
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
	#endregion

	#region UI Rendering - Main Flow
	private void ProcessEntitiesDisplay(Query entities)
	{
		if (ImGui.BeginChild("EntityScrollRegion", new NumVec2(0, 0), ImGuiChildFlags.None))
		{
			foreach (Entity entity in entities)
			{
				if (IsFiltered(entity.Name(), _searchFilter)) continue;
				ImGui.PushID(entity.GetHashCode() + 2);
				DisplayEntity(entity);
				ImGui.PopID();
			}
		}
		ImGui.EndChild();
	}

	private void DisplayEntity(Entity entity)
	{
		if (ImGui.CollapsingHeader($"[{entity.ToRaw()}] {entity.Name()}", ImGuiTreeNodeFlags.None))
		{
			ImGui.Indent();
			DisplayResources(entity);
			ImGui.Unindent();
		}
	}

	private void DisplayResources(Entity entity, HashSet<Entity> visited = null, int depth = 0)
	{
		visited ??= [];
		if (!visited.Add(entity)) return; 

		var snapshot = GetSnapshot(entity);

		ShowComponents(entity, snapshot.Components);
		ShowTags(entity, snapshot.Tags);
		ShowRelationships(entity, visited, depth, snapshot.Relationships);
	}
	#endregion

	#region UI Rendering - Sections
	private void ShowComponents(Entity entity, IReadOnlyList<EntComponentSnapshot> components)
	{
		if (components.Count == 0) return;
		
		ImGui.PushID(entity.GetHashCode() + 1); 
		bool isOpen = ImGui.CollapsingHeader("Components", ImGuiTreeNodeFlags.None);
		
		if (isOpen)
		{
			string filter = HandleEntitySearch("Search Components:", entity, _entitiesComponentFilter);
			ImGui.Indent();
			foreach (var comp in components)
			{
				if (IsFiltered(comp.DisplayName, filter)) continue;
				DisplayComponent(comp);
			}
			ImGui.Unindent();
		}
		ImGui.PopID();
	}

	private void ShowTags(Entity entity, IReadOnlyList<EntTagSnapshot> tags)
	{
		if (tags.Count == 0) return;
		
		ImGui.PushID(entity.GetHashCode() + 2);
		bool isOpen = ImGui.CollapsingHeader("Tags", ImGuiTreeNodeFlags.None);
		
		if (isOpen)
		{
			string filter = HandleEntitySearch("Search Tags:", entity, _entitiesTagsFilter);
			ImGui.Indent();

			float availableWidth = ImGui.GetContentRegionAvail().X;
			float currentWidth = 0.0f;
			bool firstInRow = true;

			foreach (var tag in tags)
			{
				if (!IsFiltered(tag.DisplayName, filter))
					DisplayTag(tag.DisplayName, tag.Color, ref firstInRow, ref currentWidth, availableWidth);
			}
			ImGui.Unindent();
		}
		ImGui.PopID();
	}

	private void ShowRelationships(Entity entity, HashSet<Entity> visited, int depth, IReadOnlyList<EntRelationshipSnapshot> relationships)
	{
		if (depth > MAX_RELATIONSHIP_DEPTH || relationships.Count == 0) return;
		
		ImGui.PushID(entity.GetHashCode() + 3);
		bool isOpen = ImGui.CollapsingHeader($"Relationships ({relationships.Count})", ImGuiTreeNodeFlags.None);
		
		if (isOpen)
		{
			string filter = HandleEntitySearch("Search Relationships:", entity, _entitiesRelationshipsFilter);
			ImGui.Indent();
			
			foreach (var rel in relationships)
			{
				if (IsFiltered(rel.Label, filter)) continue;

				string nodeLabel = $"{rel.Label} -> [{rel.Target.ToRaw()}] {rel.Target.Name()}";
				if (visited.Contains(rel.Target))
				{
					ImGui.TextColored(Amber, $"{nodeLabel} (Circular Reference)");
					continue;
				}

				if (ImGui.TreeNode(nodeLabel))
				{
					visited.Add(rel.Target);
					ImGui.Indent();
					DisplayResources(rel.Target, visited, depth + 1);
					ImGui.Unindent();
					visited.Remove(rel.Target);
					ImGui.TreePop();
				}
			}
			ImGui.Unindent();
		}
		ImGui.PopID();
	}
	#endregion

	#region UI Rendering - Elements & Helpers
	private void DisplayComponent(EntComponentSnapshot comp)
	{
		ImGui.PushStyleColor(ImGuiCol.Header, comp.Color);
		bool isOpen = ImGui.CollapsingHeader(comp.DisplayName, ImGuiTreeNodeFlags.None);
		ImGui.PopStyleColor();

		if (!isOpen) return;
		
		ImGui.Indent();
		var cache = GetOrCacheType(comp.Type);
		
		foreach (var field in cache.Fields)
			DisplayValue(field.Name, field.GetValue(comp.Value));

		foreach (var prop in cache.Properties)
		{
			if (prop.GetIndexParameters().Length == 0 && prop.CanRead)
				DisplayValue(prop.Name, prop.GetValue(comp.Value));
		}
		ImGui.Unindent();
	}

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

	private string HandleEntitySearch(string title, Entity entity, Dictionary<Entity, string> filterDict)
	{
		if (!filterDict.TryGetValue(entity, out string filter))
		{
			filter = "";
			filterDict[entity] = filter;
		}
		
		HandleSearch(title, entity.GetHashCode(), ref filter);
		
		filterDict[entity] = filter;
		return filter;
	}
	#endregion

	#region Public API & Lifecycle
	public int Priority => SPriority.Lowest;

	public void Init()
	{
		Scheduler.Register(this);
		ImGuiGD.Connect(() =>
		{
			ImGui.Begin(TITLE);
			HandleSearch("Search Entities:", 0, ref _searchFilter); 
			ImGui.Separator();
			ProcessEntitiesDisplay(world.All);
			ImGui.End();
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
		_despawningEntities.For((in EntityRef entity, ref MarkDespawn _) =>
		{
			_entitiesComponentFilter.Remove(entity.Entity);
			_entitiesTagsFilter.Remove(entity.Entity);
			_entitiesRelationshipsFilter.Remove(entity.Entity);
		});
	#endregion
}
