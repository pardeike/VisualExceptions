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
			if (ExceptionState.configuration.UseSound)
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
	}
}
