using RimWorld;
using UnityEngine;
using Verse;

namespace VisualExceptions
{
	[DefOf]
	public static class Tab
	{
		public static MainButtonDef VisualExceptionsTab;

		static Tab() => DefOfHelper.EnsureInitializedInCtor(typeof(Tab));

		internal static MainButtonDef Def => VisualExceptionsTab;

		// MainButtonsRoot sorts by order when a game is created or loaded.
		internal static void ApplyOrder()
		{
			if (Def != null)
				Def.order = ExceptionState.configuration.TabToTheRight ? 100000 : -100000;
		}

		// Main menu has no button bar, so open the window directly.
		internal static void ShowInMainMenu()
		{
			if (Def == null || Find.WindowStack.IsOpen<ExceptionInspector>())
				return;
			Find.WindowStack.Add(new ExceptionInspector());
			Tools.PlayErrorSound();
		}
	}

	[StaticConstructorOnStartup]
	static class TabStartup
	{
		static TabStartup() => Tab.ApplyOrder();
	}

	public class TabButtonWorker : MainButtonWorker_ToggleTab
	{
		public override bool Visible => ExceptionState.HasExceptions && base.Visible;

		public override void DoButton(Rect rect)
		{
			if (ExceptionState.TakeAttention())
				Tools.PlayErrorSound();
			base.DoButton(rect);
		}
	}
}
