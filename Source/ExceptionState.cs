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
		internal static Configuration configuration = new Configuration();

		static readonly Dictionary<ExceptionInfo, int> exceptions = new Dictionary<ExceptionInfo, int>(new ExceptionInfo.Comparer());
		static readonly HashSet<ExceptionInfo> bannedExceptions = new HashSet<ExceptionInfo>(new ExceptionInfo.Comparer());
		internal static Dictionary<ExceptionInfo, int> Exceptions => exceptions;

		internal static Exception Handle(Exception exception)
		{
			var result = Lookup(exception);
			if (result != null && result.Item2 == 1)
				Tab.AddExceptions();
			return exception;
		}

		internal static void Clear()
		{
			exceptions.Clear();
		}

		internal static void Load()
		{
			try
			{
				if (File.Exists(configurationPath))
				{
					var serializer = new DataContractJsonSerializer(typeof(Configuration));
					using (var stream = new FileStream(configurationPath, FileMode.Open))
						configuration = (Configuration)serializer.ReadObject(stream);
				}
				else
					configuration = new Configuration { Debugging = true };
			}
			catch
			{
				configuration = new Configuration();
			}
		}

		internal static void Save()
		{
			try
			{
				var serializer = new DataContractJsonSerializer(typeof(Configuration));
				using (var stream = new FileStream(configurationPath, FileMode.OpenOrCreate))
					serializer.WriteObject(stream, configuration);
			}
			finally
			{
			}
		}

		static Tuple<ExceptionInfo, int> Lookup(Exception exception)
		{
			var info = new ExceptionInfo(exception);
			if (bannedExceptions.Contains(info)) return null;
			if (exceptions.TryGetValue(info, out var count) == false)
				count = 0;
			exceptions[info] = ++count;
			return new Tuple<ExceptionInfo, int>(info, count);
		}

		internal static void Remove(ExceptionInfo info)
		{
			_ = exceptions.Remove(info);
		}

		internal static void Ban(ExceptionInfo info)
		{
			_ = bannedExceptions.Add(info);
		}
	}
}
