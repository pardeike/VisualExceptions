using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace VisualExceptions
{
	static class Patcher
	{
		internal static bool patchesApplied = false;

		internal static void Apply(Harmony harmony)
		{
			_ = new PatchClassProcessor(harmony, typeof(RunloopExceptionHandler)).Patch();
			_ = new PatchClassProcessor(harmony, typeof(ShowLoadingExceptions)).Patch();
			_ = new PatchClassProcessor(harmony, typeof(AddHarmonyTabWhenNecessary)).Patch();
			_ = new PatchClassProcessor(harmony, typeof(RememberHarmonyIDs)).Patch();
			patchesApplied = true;
		}
	}

	// draw the harmony lib version at the start screen
	// Note: always patched from HarmonyMain()
	//
	[HarmonyPatch(typeof(VersionControl))]
	[HarmonyPatch(nameof(VersionControl.DrawInfoInCorner))]
	static class ShowHarmonyVersionOnMainScreen
	{
		internal static void Postfix()
		{
			Tools.DrawInfoSection();
		}
	}

	// adds exception handlers
	//
	[HarmonyPatch]
	static class RunloopExceptionHandler
	{
		static readonly MethodInfo Handle = SymbolExtensions.GetMethodInfo(() => ExceptionState.Handle(null));

		[HarmonyPriority(int.MaxValue)]
		internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var list = instructions.ToList();
			var idx = 0;
			var found = false;
			try
			{
				while (true)
				{
					idx = list.FindIndex(idx, IsCatchException);
					if (idx < 0) break;
					var code = list[idx];
					if (code.opcode == OpCodes.Pop)
					{
						idx += 1;
						continue;
					}
					list.Insert(idx, new CodeInstruction(OpCodes.Call, Handle) { blocks = code.blocks, labels = code.labels });
					code.labels = new List<Label>();
					code.blocks = new List<ExceptionBlock>();
					found = true;
					idx += 2;
				}
			}
			catch (Exception ex)
			{
				Log.Error($"Transpiler Exception: {ex}");
			}
			if (found == false) return null;
			return list.AsEnumerable();
		}

		internal static IEnumerable<MethodBase> TargetMethods()
		{
			var methods = PatchPersistence.Methods;
			if (methods.Any()) return methods;
			methods = typeof(Pawn).Assembly.GetTypes()
				.Where(t => t.IsGenericType == false && (t.FullName.StartsWith("Verse.") || t.FullName.StartsWith("RimWorld.") || t.FullName.StartsWith("RuntimeAudioClipLoader.")))
				.SelectMany(t => AccessTools.GetDeclaredMethods(t))
				.Where(m => m.IsGenericMethod == false && HasCatch(m));
			PatchPersistence.Methods = methods;
			return methods;
		}

		static bool IsCatchException(CodeInstruction code)
		{
			return code.blocks.Any(block => block.blockType == ExceptionBlockType.BeginCatchBlock && block.catchType == typeof(Exception));
		}

		static bool HasCatch(MethodBase method)
		{
			try
			{
				var result = PatchProcessor.GetOriginalInstructions(method).Any(IsCatchException);
				return result;
			}
			catch
			{
				return false;
			}
		}
	}

	// opens exception window on main screen in case there is something to report
	//
	[HarmonyPatch(typeof(MainMenuDrawer))]
	[HarmonyPatch(nameof(MainMenuDrawer.MainMenuOnGUI))]
	static class ShowLoadingExceptions
	{
		static bool firstTime = true;

		internal static void Postfix()
		{
			if (firstTime && ExceptionState.Exceptions.Count > 0)
			{
				firstTime = false;
				Tab.AddExceptions_Entry();
			}
		}
	}

	// adds harmony tab to the leftmost position on the bottom of the screen
	// in case there is something to report
	//
	[HarmonyPatch(typeof(UIRoot_Play))]
	[HarmonyPatch(nameof(UIRoot_Play.Init))]
	static class AddHarmonyTabWhenNecessary
	{
		internal static void Postfix()
		{
			if (ExceptionState.Exceptions.Count > 0)
				Tab.AddExceptions();
		}
	}

	// remember all new Harmony IDs
	//
	[HarmonyPatch(typeof(Harmony))]
	[HarmonyPatch(MethodType.Constructor)]
	[HarmonyPatch(new[] { typeof(string) })]
	static class RememberHarmonyIDs
	{
		static readonly MethodInfo mPostfix = SymbolExtensions.GetMethodInfo(() => Postfix(null));

		internal static void Postfix(string id)
		{
			if (Mods.ActiveHarmonyIDs.Values.Contains(id))
				return;

			var frames = new StackTrace(false).GetFrames();
			var i = 0;
			while (i < frames.Length)
			{
				var method = Harmony.GetMethodFromStackframe(frames[i++]);
				if (method == mPostfix)
					break;
			}
			i++;
			if (i >= frames.Length) return;
			var assembly = frames[i].GetMethod().DeclaringType.Assembly;
			if (assembly != null)
				Mods.ActiveHarmonyIDs[assembly] = id;
		}
	}
}
