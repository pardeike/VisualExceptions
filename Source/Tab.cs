using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VisualExceptions
{
	public class Tab : MainButtonDef
	{
		internal static Tab instance = new Tab();
		internal static readonly AccessTools.FieldRef<MainButtonsRoot, List<MainButtonDef>> allButtonsInOrderRef = AccessTools.FieldRefAccess<MainButtonsRoot, List<MainButtonDef>>("allButtonsInOrder");
		internal static readonly AccessTools.FieldRef<MainButtonDef, Texture2D> iconRef = AccessTools.FieldRefAccess<MainButtonDef, Texture2D>("icon");

		public Tab() : base()
		{
			tabWindowClass = typeof(ExceptionInspector);
			defName = "harmony";
			description = "Harmony";
			order = -99999;
			validWithoutMap = true;
			minimized = true;
		}

		internal static void AddExceptions()
		{
			var root = Find.UIRoot;
			if (root == null) return;

			if (root is UIRoot_Play rootPlay)
			{
				AddExceptions_Play(rootPlay);
				return;
			}

			if (root is UIRoot_Entry rootEntry)
			{
				AddExceptions_Entry();
				return;
			}
		}

		internal static void AddExceptions_Play(UIRoot_Play rootPlay)
		{
			var allTabs = allButtonsInOrderRef(rootPlay.mainButtonsRoot);
			if (allTabs.Contains(instance) == false)
			{
				iconRef(instance) = Assets.harmonyTab;
				allTabs.Insert(0, instance);
				Tools.PlayErrorSound();
			}
		}

		internal static void AddExceptions_Entry()
		{
			if (Find.WindowStack.IsOpen<ExceptionInspector>() == false)
			{
				iconRef(instance) = Assets.harmonyTab;
				Find.WindowStack.Add(new ExceptionInspector
				{
					def = instance,
					customPosition = new Vector2(10, 10)
				});
				Tools.PlayErrorSound();
			}
		}
	}
}
