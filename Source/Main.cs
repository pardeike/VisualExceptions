using Brrainz;
using HarmonyLib;
using System.Linq;
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

			CrossPromotion.Install(76561197973010050);
		}

		public HarmonyMain(ModContentPack content) : base(content) { }

		public override void DoSettingsWindowContents(Rect inRect)
		{
			var list = new Listing_Standard { ColumnWidth = inRect.width / 2f };
			list.Begin(inRect);
			list.Gap(12f);

			var conf = ExceptionState.configuration;
			var props = AccessTools.GetDeclaredProperties(typeof(Configuration));
			var values = props.Select(prop => (prop, prop.TryGetAttribute<SettingsAttribute>(), (bool)prop.GetValue(conf))).ToArray();
			var needsSave = false;
			foreach (var value in values)
			{
				var temp = value.Item3;
				list.CheckboxLabeled(value.Item2.label, ref temp);
				if (temp != value.Item3)
				{
					needsSave = true;
					value.prop.SetValue(conf, temp);
				}
			}
			if (needsSave)
				ExceptionState.Save();

			list.End();
		}

		public override string SettingsCategory()
		{
			return "Visual Exceptions";
		}
	}
}
