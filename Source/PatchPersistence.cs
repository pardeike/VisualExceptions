using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Verse;

namespace VisualExceptions
{
	static class PatchPersistence
	{
		static readonly string configurationPath = Path.Combine(GenFilePaths.ConfigFolderPath, "HarmonyModPatches.json");

		internal static IEnumerable<MethodBase> Methods
		{
			// caching is disabled for now
			//
			get
			{
				return new List<MethodBase>();
				/*
				if (File.Exists(configurationPath) == false)
					return new List<MethodBase>();
				try
				{
					var serializer = new DataContractJsonSerializer(typeof(JSONMethods));
					using (var stream = new FileStream(configurationPath, FileMode.Open))
					{
						var container = (JSONMethods)serializer.ReadObject(stream);
						var methods = container.ToMethods();
						_ = methods.ToList();
						return methods;
					}
				}
				catch
				{
					return new List<MethodBase>();
				}
				*/
			}
			set
			{
				/*
				try
				{
					var serializer = new DataContractJsonSerializer(typeof(JSONMethods));
					using (var stream = new FileStream(configurationPath, FileMode.OpenOrCreate))
						serializer.WriteObject(stream, JSONMethods.Serialize(value));
				}
				finally
				{
				}
				*/
			}
		}

		internal static void ClearMethods()
		{
			if (File.Exists(configurationPath))
				File.Delete(configurationPath);
		}
	}
}
