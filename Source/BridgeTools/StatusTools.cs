using RimBridgeServer.Sdk;
using Verse;

namespace VisualExceptions;

public sealed class StatusTools
{
	[Tool(
		"visualexceptions/get_status",
		Description = "Read a compact live Visual Exceptions status without changing the game.")]
	public static object GetStatus()
	{
		var map = Find.CurrentMap;
		return new
		{
			success = true,
			mod = "Visual Exceptions",
			packageId = "brrainz.visualexceptions",
			modVersion = typeof(StatusTools).Assembly.GetName().Version?.ToString(),
			programState = Current.ProgramState.ToString(),
			hasCurrentMap = map != null,
			currentMap = map == null
				? null
				: new
				{
					id = map.uniqueID,
					size = new { x = map.Size.x, z = map.Size.z }
				}
		};
	}
}
