using UnityEngine;
using Verse;

namespace VisualExceptions
{
	[StaticConstructorOnStartup]
	class HarmonyMain : Mod
	{
		static HarmonyMain() // loads earliest
		{
			ExceptionState.Load();
			if (ExceptionState.configuration.Debugging)
				Patcher.Apply();
		}

		public HarmonyMain(ModContentPack content) : base(content) { }

		public override void DoSettingsWindowContents(Rect inRect)
		{
			var list = new Listing_Standard { ColumnWidth = inRect.width / 2f };
			list.Begin(inRect);
			list.Gap(12f);

			var conf = ExceptionState.configuration;
			var debugging = conf.Debugging;
			var tabToTheRight = conf.TabToTheRight;
			var useSound = conf.UseSound;

			list.CheckboxLabeled("Enabled", ref debugging);
			list.CheckboxLabeled("Show exceptions to the right", ref tabToTheRight);
			list.CheckboxLabeled("Use sound", ref useSound);

			if (debugging != conf.Debugging || tabToTheRight != conf.TabToTheRight || useSound != conf.UseSound)
			{
				conf.Debugging = debugging;
				conf.TabToTheRight = tabToTheRight;
				conf.UseSound = useSound;
				ExceptionState.Save();
			}

			list.End();
		}

		public override string SettingsCategory()
		{
			return "Visual Exceptions";
		}
	}
}
