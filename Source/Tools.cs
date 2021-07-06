using HarmonyLib;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VisualExceptions
{
	struct ColumnInfo { internal Action draw; internal float dim; }
	delegate ColumnInfo ColumnDrawer(Rect rect);

	static class Tools
	{
		static readonly Rect debugLabelRect = new Rect(28f, 16f, 212f, 20f);
		static readonly Rect debugImageRect = new Rect(0f, 20f, 23f, 12f);
		static readonly Rect debugButtonRect = new Rect(0f, 16f, 240f, 36f);
		internal static readonly Assembly HarmonyModAssembly = typeof(Tools).Assembly;
		internal static readonly string RimworldAssemblyName = typeof(Pawn).Assembly.GetName().Name;
		internal static AudioSource audioSource = null;

		internal static string GetTexturesPath(string fileName)
		{
			var root = LoadedModManager.GetMod<HarmonyMain>()?.Content.RootDir ?? "";
			return Path.Combine(root, "Textures", fileName);
		}

		internal static AudioSource GetAudioSource()
		{
			if (audioSource == null)
			{
				var gameObject = new GameObject("HarmonyOneShotSourcesWorldContainer");
				gameObject.transform.position = Vector3.zero;
				var gameObject2 = new GameObject("HarmonyOneShotSource");
				gameObject2.transform.parent = gameObject.transform;
				gameObject2.transform.localPosition = Vector3.zero;
				audioSource = AudioSourceMaker.NewAudioSourceOn(gameObject2);
			}
			return audioSource;
		}

		internal static void PlayErrorSound()
		{
			GetAudioSource().PlayOneShot(Assets.error);
		}

		internal static void Iterate<T>(this IEnumerable<T> collection, Action<T, int> action)
		{
			var i = 0;
			foreach (var item in collection)
				action(item, i++);
		}

		internal static string GetAuthor(this ModMetaData metaData)
		{
			var s1 = Traverse.Create(metaData).Property("AuthorsString").GetValue<string>();
			var s2 = Traverse.Create(metaData).Property("Author").GetValue<string>();
			return s1 ?? s2 ?? "";
		}

		internal static string ShortDescription(this Type type)
		{
			if (type is null) return "null";
			var result = type.Name;
			if (type.IsGenericType)
			{
				result += "<";
				var subTypes = type.GetGenericArguments();
				for (var i = 0; i < subTypes.Length; i++)
				{
					if (result.EndsWith("<", StringComparison.Ordinal) is false)
						result += ", ";
					result += subTypes[i].ShortDescription();
				}
				result += ">";
			}
			return result;
		}

		internal static string ShortDescription(this MethodBase member)
		{
			if (member is null) return "null";
			var returnType = AccessTools.GetReturnedType(member);
			var result = new StringBuilder();
			if (member.IsStatic) _ = result.Append("static ");
			if (member.IsAbstract) _ = result.Append("abstract ");
			if (member.IsVirtual) _ = result.Append("virtual ");
			if (returnType != typeof(void))
				_ = result.Append($"{returnType.ShortDescription()} ");
			if (member.DeclaringType is object)
				_ = result.Append($"{member.DeclaringType.ShortDescription()}.");
			var parameterString = member.GetParameters().Join(p => $"{p.ParameterType.ShortDescription()} {p.Name}");
			_ = result.Append($"{member.Name} ({parameterString})");
			return result.ToString();
		}

		// 1.2 public FloatMenuOption(string label, Action action, MenuOptionPriority priority = MenuOptionPriority.Default, Action mouseoverGuiAction = null,       Thing revalidateClickTarget = null, float extraPartWidth = 0f, Func<Rect, bool> extraPartOnGUI = null, WorldObject revalidateWorldClickTarget = null)
		// 1.3 public FloatMenuOption(string label, Action action, MenuOptionPriority priority = MenuOptionPriority.Default, Action<Rect> mouseoverGuiAction = null, Thing revalidateClickTarget = null, float extraPartWidth = 0f, Func<Rect, bool> extraPartOnGUI = null, WorldObject revalidateWorldClickTarget = null, bool playSelectionSound = true, int orderInPriority = 0)
		//
		private static ConstructorInfo cFloatMenuOption1 = null;
		private static object[] floatMenuOptionDefaults1 = new object[0];
		internal static FloatMenuOption NewFloatMenuOption(string label, Action action, MenuOptionPriority priority = MenuOptionPriority.Default, Action mouseoverGuiAction = null, Thing revalidateClickTarget = null, float extraPartWidth = 0f, Func<Rect, bool> extraPartOnGUI = null, WorldObject revalidateWorldClickTarget = null)
		{
			if (cFloatMenuOption1 == null)
			{
				cFloatMenuOption1 = AccessTools.GetDeclaredConstructors(typeof(FloatMenuOption), false)
					.First(c => c.GetParameters().ToList().Any(p => p.ParameterType == typeof(MenuOptionPriority)));
				floatMenuOptionDefaults1 = cFloatMenuOption1.GetParameters().Select(p => AccessTools.GetDefaultValue(p.ParameterType)).ToArray();
			}
			var parameters = floatMenuOptionDefaults1;
			var isActionRect = cFloatMenuOption1.GetParameters()[3].ParameterType == typeof(Action<Rect>);
			void actionRect(Rect r) { mouseoverGuiAction?.Invoke(); }
			parameters[0] = label;
			parameters[1] = action;
			parameters[2] = priority;
			parameters[3] = isActionRect ? (object)(Action<Rect>)actionRect : mouseoverGuiAction;
			parameters[4] = revalidateClickTarget;
			parameters[5] = extraPartWidth;
			parameters[6] = extraPartOnGUI;
			parameters[7] = revalidateWorldClickTarget;
			return (FloatMenuOption)cFloatMenuOption1.Invoke(parameters);
		}

		// 1.2 public FloatMenuOption(string label, Action action, Texture2D itemIcon, Color iconColor, MenuOptionPriority priority = MenuOptionPriority.Default, Action mouseoverGuiAction = null,       Thing revalidateClickTarget = null, float extraPartWidth = 0f, Func<Rect, bool> extraPartOnGUI = null, WorldObject revalidateWorldClickTarget = null)
		// 1.3 public FloatMenuOption(string label, Action action, Texture2D itemIcon, Color iconColor, MenuOptionPriority priority = MenuOptionPriority.Default, Action<Rect> mouseoverGuiAction = null, Thing revalidateClickTarget = null, float extraPartWidth = 0f, Func<Rect, bool> extraPartOnGUI = null, WorldObject revalidateWorldClickTarget = null, bool playSelectionSound = true, int orderInPriority = 0)
		//
		private static ConstructorInfo cFloatMenuOption2 = null;
		private static object[] floatMenuOptionDefaults2 = new object[0];
		internal static FloatMenuOption NewFloatMenuOption(string label, Action action, Texture2D itemIcon, Color iconColor, MenuOptionPriority priority = MenuOptionPriority.Default, Action mouseoverGuiAction = null, Thing revalidateClickTarget = null, float extraPartWidth = 0f, Func<Rect, bool> extraPartOnGUI = null, WorldObject revalidateWorldClickTarget = null)
		{
			if (cFloatMenuOption2 == null)
			{
				cFloatMenuOption2 = AccessTools.GetDeclaredConstructors(typeof(FloatMenuOption), false)
					.First(c => c.GetParameters().ToList().Any(p => p.ParameterType == typeof(Texture2D)));
				floatMenuOptionDefaults2 = cFloatMenuOption2.GetParameters().Select(p => AccessTools.GetDefaultValue(p.ParameterType)).ToArray();
			}
			var parameters = floatMenuOptionDefaults2;
			var isActionRect = cFloatMenuOption2.GetParameters()[5].ParameterType == typeof(Action<Rect>);
			void actionRect(Rect r) { mouseoverGuiAction?.Invoke(); }
			parameters[0] = label;
			parameters[1] = action;
			parameters[2] = itemIcon;
			parameters[3] = iconColor;
			parameters[4] = priority;
			parameters[5] = isActionRect ? (object)(Action<Rect>)actionRect : mouseoverGuiAction;
			parameters[6] = revalidateClickTarget;
			parameters[7] = extraPartWidth;
			parameters[8] = extraPartOnGUI;
			parameters[9] = revalidateWorldClickTarget;
			return (FloatMenuOption)cFloatMenuOption2.Invoke(parameters);
		}

		internal static void Button(Texture2D texture, Rect rect, string tipKey, bool highlight, Action action)
		{
			_ = highlight;
			if (texture != null)
			{
				var oldColor = GUI.color;
				GUI.color = Mouse.IsOver(rect) ? Color.gray : Color.white;
				GUI.DrawTexture(rect, texture);
				GUI.color = oldColor;
			}
			TooltipHandler.TipRegionByKey(rect, tipKey);
			if (Widgets.ButtonInvisible(rect)) action();
		}

		internal static void DrawInfoSection()
		{
			var oldFont = Text.Font;
			var oldColor = GUI.color;
			Text.Font = GameFont.Small;

			GUI.BeginGroup(new Rect(10f, 74f, 240f, 40f));
			GUI.color = new Color(1f, 1f, 1f, 0.5f);
			var devText = "DevelopmentMode".Translate().ToString();
			var devTextLen = devText.Size().x;
			Widgets.Label(debugLabelRect, devText);
			GUI.color = Color.white;
			GUI.DrawTexture(debugImageRect, Assets.debugToggle[ExceptionState.configuration.Debugging ? 1 : 0]);
			if (Patcher.patchesApplied != ExceptionState.configuration.Debugging && DateTime.Now.Second % 2 == 0)
			{
				var rect = new Rect(debugImageRect.xMax + devTextLen + 10, debugImageRect.y - 2, debugImageRect.height + 4, debugImageRect.height + 4);
				GUI.DrawTexture(rect, Assets.restart);
			}
			if (Widgets.ButtonInvisible(debugButtonRect, false))
			{
				ExceptionState.configuration.Debugging = !ExceptionState.configuration.Debugging;
				if (ExceptionState.configuration.Debugging)
					PatchPersistence.ClearMethods();
				ExceptionState.Save();
			}
			GUI.EndGroup();

			GUI.color = oldColor;
			Text.Font = oldFont;
		}
	}
}
