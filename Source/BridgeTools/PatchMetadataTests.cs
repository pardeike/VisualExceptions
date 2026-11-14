using HarmonyLib;
using RimBridgeServer.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace VisualExceptions;

public sealed class PatchMetadataTests
{
	const string Owner = "net.pardeike.visualexceptions.patch-metadata-tests";
	const string Message = "Visual Exceptions generated-patch regression test";

	[Tool("visualexceptions/validate_patch_metadata",
		Description = "Test patch ownership across every Harmony patch category and build an exception report with a generated prefix. Temporary patches are removed afterwards.")]
	public static object Run(
		[ToolParameter(Description = "Keep the generated exception in the inspector for a rendering check", DefaultValue = false)]
		bool showInspector = false)
	{
		var generated = CreatePrefix();
		var owned = AccessTools.Method(typeof(Tab), nameof(Tab.ApplyOrder));
		var unowned = AccessTools.Method(typeof(PatchMetadataTests), nameof(NoOp));
		var patches = new[]
		{
			new Patch(new HarmonyMethod(generated), 0, Owner),
			new Patch(new HarmonyMethod(unowned), 1, Owner),
			new Patch(new HarmonyMethod(owned), 2, Owner),
			new Patch(new HarmonyMethod(owned), 3, Patcher.harmony_id)
		};
		var info = new Patches(patches, patches, patches, patches, patches, patches, patches);
		var metadata = Mods.GetModMetaData(owned.DeclaringType.Assembly);
		var cases = new List<CaseResult>
		{
			Check("prefixes", Mods.GetPrefixes(info), owned, metadata, 1),
			Check("postfixes", Mods.GetPostfixes(info), owned, metadata, 1),
			Check("transpilers", Mods.GetTranspilers(info), owned, metadata, 1),
			Check("finalizers", Mods.GetFinalizers(info), owned, metadata, 1),
			Check("inner patches without finalizers", Mods.GetInnerPatches(info, false), owned, metadata, 2),
			Check("inner patches with finalizers", Mods.GetInnerPatches(info, true), owned, metadata, 3),
			Check("no patches", Mods.GetPrefixes(null).Concat(Mods.GetPostfixes(null))
				.Concat(Mods.GetTranspilers(null)).Concat(Mods.GetFinalizers(null))
				.Concat(Mods.GetInnerPatches(null, true)), owned, metadata, 0),
			CheckReport(generated, showInspector)
		};
		return new
		{
			success = cases.All(item => item.success),
			generatedAssembly = generated.DeclaringType.Assembly.FullName,
			ownedMod = metadata?.PackageId,
			harmonyAssembly = typeof(Harmony).Assembly.Location,
			harmonyVersion = typeof(Harmony).Assembly.GetName().Version?.ToString(),
			cases
		};
	}

	static CaseResult Check(string name, IEnumerable<ModInfo> entries, MethodInfo owned, ModMetaData metadata, int expected)
	{
		var actual = entries.ToArray();
		return new CaseResult
		{
			name = name,
			success = metadata != null && actual.Length == expected
				&& actual.All(entry => entry.method == owned && entry.metaData == metadata),
			expected = expected,
			actual = actual.Length
		};
	}

	static CaseResult CheckReport(MethodInfo generated, bool showInspector)
	{
		var target = AccessTools.Method(typeof(Tools), nameof(Tools.GetTexturesPath));
		var harmony = new Harmony(Owner);
		try
		{
			harmony.Patch(target, prefix: new HarmonyMethod(generated));
			Exception captured = null;
			try { target.Invoke(null, new object[] { "unused" }); }
			catch (TargetInvocationException exception) when (exception.InnerException?.Message == Message)
			{
				captured = exception.InnerException;
			}
			if (captured == null)
				throw new InvalidOperationException("The generated prefix did not throw.");

			var report = new ExceptionInfo(captured).GetReport();
			// Column creation reads metadata before drawing. This is the issue #7 crash.
			foreach (var mod in report.mods)
				_ = mod.Column();
			if (showInspector)
			{
				_ = ExceptionState.Handle(captured);
				foreach (var entry in ExceptionState.Snapshot())
					_ = entry.Key.GetReport();
				Tab.ShowInMainMenu();
			}
			return new CaseResult { name = "generated prefix exception report", success = report.mods.Count == 0, expected = 0, actual = report.mods.Count };
		}
		catch (Exception exception)
		{
			return new CaseResult { name = "generated prefix exception report", error = exception.ToString() };
		}
		finally
		{
			harmony.Unpatch(target, HarmonyPatchType.Prefix, Owner);
		}
	}

	static MethodInfo CreatePrefix()
	{
		var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("VisualExceptions.GeneratedPatchTest"), AssemblyBuilderAccess.Run);
		var type = assembly.DefineDynamicModule("Patches").DefineType("GeneratedPrefix", TypeAttributes.Public);
		var method = type.DefineMethod("Prefix", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
		var il = method.GetILGenerator();
		il.Emit(OpCodes.Ldstr, Message);
		il.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) }));
		il.Emit(OpCodes.Throw);
		return type.CreateType().GetMethod("Prefix");
	}

	static void NoOp() { }

	sealed class CaseResult
	{
		public string name;
		public bool success;
		public int expected;
		public int actual;
		public string error;
	}
}
