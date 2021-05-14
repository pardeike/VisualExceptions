using HarmonyLib;
using Verse;

namespace VisualExceptions
{
	[StaticConstructorOnStartup]
	class HarmonyMain : Mod
	{
		internal static string harmony_id = "net.pardeike.rimworld.lib.harmony";

		static HarmonyMain()
		{
			var harmony = new Harmony(harmony_id);
			_ = new PatchClassProcessor(harmony, typeof(ShowHarmonyVersionOnMainScreen)).Patch();

			ExceptionState.Load();
			if (ExceptionState.configuration.Debugging)
				Patcher.Apply(harmony);
		}

		public HarmonyMain(ModContentPack content) : base(content) { }
	}
}
