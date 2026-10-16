using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using Verse;

namespace VisualExceptions
{
	static class ExceptionState
	{
		static readonly string configurationPath = Path.Combine(GenFilePaths.ConfigFolderPath, "VisualExceptionsSettings.json");
		internal static Configuration configuration = new();

		// Handle runs inside game catch blocks, on any thread. It only records;
		// the UI picks the state up on the main thread.
		static readonly object sync = new();
		static readonly Dictionary<ExceptionInfo, int> exceptions = new(new ExceptionInfo.Comparer());
		static readonly HashSet<ExceptionInfo> bannedExceptions = new(new ExceptionInfo.Comparer());
		static volatile bool needsAttention;

		internal static Exception Handle(Exception exception)
		{
			try
			{
				var info = new ExceptionInfo(exception);
				info.Analyze();
				lock (sync)
				{
					if (bannedExceptions.Contains(info)) return exception;
					if (exceptions.Count == 0)
						needsAttention = true;
					_ = exceptions.TryGetValue(info, out var count);
					exceptions[info] = count + 1;
				}
			}
			catch
			{
				// Never throw from inside the game's own exception handling.
			}
			return exception;
		}

		internal static bool HasExceptions
		{
			get { lock (sync) return exceptions.Count > 0; }
		}

		// True once after the list went from empty to non-empty.
		internal static bool TakeAttention()
		{
			if (needsAttention == false) return false;
			needsAttention = false;
			return HasExceptions;
		}

		internal static List<KeyValuePair<ExceptionInfo, int>> Snapshot()
		{
			lock (sync) return [.. exceptions];
		}

		internal static void Clear()
		{
			lock (sync) exceptions.Clear();
		}

		internal static void Remove(ExceptionInfo info)
		{
			lock (sync) _ = exceptions.Remove(info);
		}

		internal static void Ban(ExceptionInfo info)
		{
			lock (sync) _ = bannedExceptions.Add(info);
		}

		internal static void Load()
		{
			try
			{
				if (File.Exists(configurationPath))
				{
					var serializer = new DataContractJsonSerializer(typeof(Configuration));
					using var stream = new FileStream(configurationPath, FileMode.Open);
					configuration = (Configuration)serializer.ReadObject(stream);
				}
				else
					configuration = Defaults();
			}
			catch
			{
				configuration = Defaults();
			}
		}

		static Configuration Defaults() => new()
		{
			Debugging = true,
			TabToTheRight = false,
			UseSound = true,
			IncludeFinalizers = false
		};

		internal static void Save()
		{
			try
			{
				var serializer = new DataContractJsonSerializer(typeof(Configuration));
				using var stream = new FileStream(configurationPath, FileMode.Create);
				serializer.WriteObject(stream, configuration);
			}
			catch (Exception ex)
			{
				Log.Warning($"Visual Exceptions could not save its settings: {ex.Message}");
			}
		}
	}
}
