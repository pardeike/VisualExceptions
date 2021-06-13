using RimWorld;
using UnityEngine;
using Verse;

namespace VisualExceptions
{
	public class Tab : MainButtonDef
	{
		internal static Tab instance = new Tab();

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
			var allTabs = rootPlay.mainButtonsRoot.allButtonsInOrder;
			if (allTabs.Contains(instance) == false)
			{
				instance.icon = Assets.harmonyTab;
				allTabs.Insert(0, instance);
				Tools.PlayErrorSound();
			}
		}

		internal static void AddExceptions_Entry()
		{
			if (Find.WindowStack.IsOpen<ExceptionInspector>() == false)
			{
				instance.icon = Assets.harmonyTab;
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
