using RimBridgeServer.Sdk;
using RimWorld;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace VisualExceptions;

public sealed class VisualExceptionsTools
{
	const string DefaultTestMessage = "Visual Exceptions bridge test";

	[Tool(
		"visualexceptions/get_state",
		Description = "Read what Visual Exceptions has recorded and how its tab and window are shown, without changing the game.")]
	public static object GetState() => DescribeState();

	[Tool(
		"visualexceptions/clear_exceptions",
		Description = "Forget all recorded exceptions, as closing the Visual Exceptions window does, then return the state.")]
	public static object ClearExceptions()
	{
		ExceptionState.Clear();
		return DescribeState();
	}

	[Tool(
		"visualexceptions/raise_test_exception",
		Description = "In a loaded game, throw one deliberate exception inside a RimWorld try/catch, then return the resulting Visual Exceptions state. source=tick throws from a game component on the next tick (GameComponentTick); source=window throws while a test window draws (Window.InnerWindowOnGUI), which also puts mods that patch window drawing on the stack.")]
	public static async Task<object> RaiseTestException(
		IRimBridgeContext ctx,
		CancellationToken cancellationToken,
		[ToolParameter(Description = "Exception message", DefaultValue = DefaultTestMessage)]
		string message = DefaultTestMessage,
		[ToolParameter(Description = "Where to throw: tick or window", DefaultValue = "tick")]
		string source = "tick")
	{
		message = string.IsNullOrWhiteSpace(message) ? DefaultTestMessage : message;
		if (source == "window")
			return await RaiseInWindow(ctx, message, cancellationToken);
		if (source != "tick")
			return new { success = false, message = $"Unknown source '{source}'; use tick or window." };
		var component = new BridgeTestComponent { message = message };
		var before = await ctx.MainThread.InvokeAsync(() =>
		{
			if (Current.ProgramState != ProgramState.Playing || Current.Game == null)
				return -1;
			Current.Game.components.Add(component);
			return ExceptionState.Snapshot().Sum(entry => entry.Value);
		}, cancellationToken);
		if (before < 0)
			return new { success = false, message = "Load or start a game first.", programState = Current.ProgramState.ToString() };

		RimBridgeTickResult tick;
		try
		{
			tick = await ctx.Game.StepTicksAsync(1, cancellationToken: cancellationToken);
		}
		finally
		{
			// Never leave the test component in a game that might be saved.
			await ctx.MainThread.InvokeAsync(() => Current.Game?.components.Remove(component), CancellationToken.None);
		}

		return await ctx.MainThread.InvokeAsync(() =>
		{
			var after = ExceptionState.Snapshot().Sum(entry => entry.Value);
			return (object)new
			{
				success = component.thrown && after > before,
				thrown = component.thrown,
				recordedBefore = before,
				recordedAfter = after,
				tick = new { tick.Success, tick.CompletedTicks, tick.Message },
				visualExceptions = DescribeState()
			};
		}, cancellationToken);
	}

	static async Task<object> RaiseInWindow(IRimBridgeContext ctx, string message, CancellationToken cancellationToken)
	{
		var window = new BridgeTestWindow { message = message };
		var before = await ctx.MainThread.InvokeAsync(() =>
		{
			if (Current.ProgramState != ProgramState.Playing || Current.Game == null)
				return -1;
			Find.WindowStack.Add(window);
			return ExceptionState.Snapshot().Sum(entry => entry.Value);
		}, cancellationToken);
		if (before < 0)
			return new { success = false, message = "Load or start a game first.", programState = Current.ProgramState.ToString() };
		try
		{
			// Windows draw every frame, also while paused; no game time passes.
			await ctx.Game.FramesAsync(3, cancellationToken);
		}
		finally
		{
			await ctx.MainThread.InvokeAsync(() => Find.WindowStack?.TryRemove(window, false), CancellationToken.None);
		}
		return await ctx.MainThread.InvokeAsync(() =>
		{
			var after = ExceptionState.Snapshot().Sum(entry => entry.Value);
			return (object)new
			{
				success = window.thrown && after > before,
				thrown = window.thrown,
				recordedBefore = before,
				recordedAfter = after,
				visualExceptions = DescribeState()
			};
		}, cancellationToken);
	}

	static object DescribeState()
	{
		var playing = Current.ProgramState == ProgramState.Playing && Current.Game != null;
		var def = Tab.Def;
		var config = ExceptionState.configuration;
		var snapshot = ExceptionState.Snapshot();
		return new
		{
			success = true,
			programState = Current.ProgramState.ToString(),
			patchesApplied = Patcher.patchesApplied,
			settings = new
			{
				enabled = config.Debugging,
				tabToTheRight = config.TabToTheRight,
				useSound = config.UseSound,
				includeFinalizers = config.IncludeFinalizers
			},
			exceptionCount = snapshot.Count,
			exceptions = snapshot.Select(entry =>
			{
				var report = entry.Key.GetReport();
				return new
				{
					message = report.exceptionMessage,
					count = entry.Value,
					location = report.topMethod,
					mods = report.mods.Select(mod => new
					{
						name = mod.meta?.Name,
						packageId = mod.meta?.PackageId,
						methods = mod.methods.Select(method => method.ShortDescription()).ToArray()
					}).ToArray()
				};
			}).ToArray(),
			tab = def == null ? null : new
			{
				defName = def.defName,
				order = def.order,
				// Worker visibility reads game state that exists only in play.
				visible = playing ? def.Worker.Visible : (bool?)null,
				open = playing && Find.MainTabsRoot.OpenTab == def,
				positionInBar = playing ? BarPosition(def) : null
			},
			windowOpen = Find.WindowStack?.IsOpen<ExceptionInspector>() ?? false
		};
	}

	// Reads the live bar: RimWorld sorts it once per game, so def order alone can
	// differ from what is shown until the next load.
	static readonly FieldInfo allButtonsInOrder = typeof(MainButtonsRoot).GetField("allButtonsInOrder", BindingFlags.Instance | BindingFlags.NonPublic);

	static string BarPosition(MainButtonDef def)
	{
		if (Find.UIRoot is not UIRoot_Play root || allButtonsInOrder?.GetValue(root.mainButtonsRoot) is not System.Collections.Generic.List<MainButtonDef> buttons)
			return "unknown";
		var visible = buttons.Where(button => button.Worker.Visible).ToList();
		var index = visible.IndexOf(def);
		if (index < 0) return "hidden";
		return index == 0 ? "first" : index == visible.Count - 1 ? "last" : $"index {index} of {visible.Count}";
	}
}

// Test-only window; throws once while drawing and is removed by the tool.
public sealed class BridgeTestWindow : Window
{
	public string message;
	public bool thrown;

	public override UnityEngine.Vector2 InitialSize => new(240, 120);

	public override void DoWindowContents(UnityEngine.Rect inRect)
	{
		if (thrown) return;
		thrown = true;
		throw new InvalidOperationException(message);
	}
}

// Test-only game component; removed again right after its tick.
public sealed class BridgeTestComponent : GameComponent
{
	public string message;
	public bool thrown;

	public override void GameComponentTick()
	{
		if (thrown) return;
		thrown = true;
		Throw(message);
	}

	static void Throw(string text) => throw new InvalidOperationException(text);
}
