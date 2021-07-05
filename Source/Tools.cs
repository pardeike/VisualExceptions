using HarmonyLib;
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
