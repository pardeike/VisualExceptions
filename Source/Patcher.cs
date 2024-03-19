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
		internal static string harmony_id = "net.pardeike.rimworld.lib.harmony";

		internal static HashSet<MethodBase> ignoredMethods =
		[
			SymbolExtensions.GetMethodInfo(() => ParseHelper.FromString("", typeof(void)))
		];

		internal static void Apply()
		{
			var harmony = new Harmony(harmony_id);
			_ = new PatchClassProcessor(harmony, typeof(ExceptionsAndActivatorHandler)).Patch();
			_ = new PatchClassProcessor(harmony, typeof(ShowLoadingExceptions)).Patch();
			_ = new PatchClassProcessor(harmony, typeof(AddHarmonyTabWhenNecessary)).Patch();
			_ = new PatchClassProcessor(harmony, typeof(RememberHarmonyIDs)).Patch();
			patchesApplied = true;
		}
	}

	// adds exception handlers
	//
	[HarmonyPatch]
	static class ExceptionsAndActivatorHandler
	{
		static readonly MethodInfo Handle = SymbolExtensions.GetMethodInfo(() => ExceptionState.Handle(null));

		static readonly Dictionary<MethodInfo, MethodInfo> createInstanceMethods = new()
		{
			{ SymbolExtensions.GetMethodInfo(() => Activator.CreateInstance(typeof(void))),                SymbolExtensions.GetMethodInfo(() => PatchedActivator.CreateInstance(typeof(void), null)) },
			{ SymbolExtensions.GetMethodInfo(() => Activator.CreateInstance(typeof(void), new object[0])), SymbolExtensions.GetMethodInfo(() => PatchedActivator.CreateInstance(typeof(void), new object[0], null)) },
		};

		class PatchedActivator
		{
			static object GetInfo(object obj)
			{
				if (obj == null) return null;
				var def = obj as Def;
				if (def != null) return def;
				var fields = AccessTools.GetDeclaredFields(def.GetType());
				foreach (var field in fields)
					if (typeof(Def).IsAssignableFrom(field.FieldType))
					{
						def = field.GetValue(obj) as Def;
						if (def != null) return def;
					}
				return obj;
			}

			internal static object CreateInstance(Type type, object obj)
			{
				if (type != null) return Activator.CreateInstance(type);
				var info = GetInfo(obj);
				var message = "Activator.CreateInstance(type) called with a null type";
				if (info != null) message += $", possible context={info}";
				throw new ArgumentNullException(message);
			}

			internal static object CreateInstance(Type type, object[] objects, object me)
			{
				if (type != null) return Activator.CreateInstance(type, objects);
				var info = GetInfo(me);
				var message = $"Activator.CreateInstance(type, object[]) called with a null type, objects=[{objects.Join(o => o?.ToString() ?? "null")}]";
				if (info != null) message += $", possible context={info}";
				throw new ArgumentNullException(message);
			}
		}

		[HarmonyPriority(int.MaxValue)]
		internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
		{
			var list = instructions.ToList();

			var found = false;
			for (var i = 0; i < list.Count; i++)
				if (list[i].opcode == OpCodes.Call)
					if (list[i].operand is MethodInfo methodInfo && createInstanceMethods.TryGetValue(methodInfo, out var replacement))
					{
						if (original.IsStatic)
						{
							var parameters = original.GetParameters();
							var defIndex = parameters.Select(p => p.ParameterType).FirstIndexOf(type => type.IsGenericType == false && type.IsByRef == false && typeof(Def).IsAssignableFrom(type));
							if (defIndex >= 0 && defIndex < parameters.Length)
							{
								list.Insert(i, new CodeInstruction(OpCodes.Ldarg, defIndex));
								list[++i].operand = replacement;
								found = true;
							}
						}
						else
						{
							list.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
							list[++i].operand = replacement;
							found = true;
						}
					}

			var idx = 0;
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
					code.labels = [];
					code.blocks = [];
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
			var methods = typeof(Pawn).Assembly.GetTypes()
				.Where(t => t.IsGenericType == false && (t.FullName.StartsWith("Verse.") || t.FullName.StartsWith("RimWorld.") || t.FullName.StartsWith("RuntimeAudioClipLoader.")))
				.SelectMany(t => AccessTools.GetDeclaredMethods(t))
				.Where(m => Patcher.ignoredMethods.Contains(m) == false && m.IsGenericMethod == false && HasCatch(m));
			return methods;
		}

		static bool IsCatchException(CodeInstruction code)
		{
			return code.blocks.Any(block => block.blockType == ExceptionBlockType.BeginCatchBlock && block.catchType == typeof(Exception));
		}

		static bool HasCreateInstance(CodeInstruction code)
		{
			return code.operand is MethodInfo methodInfo && createInstanceMethods.ContainsKey(methodInfo);
		}

		static bool HasCatch(MethodBase method)
		{
			try
			{
				if (PatchProcessor.GetOriginalInstructions(method).Any(code => IsCatchException(code) || HasCreateInstance(code)))
					return true;
			}
			catch
			{
			}
			return false;
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
