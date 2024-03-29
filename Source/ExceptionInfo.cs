using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;
using Verse.Sound;
using static HarmonyLib.AccessTools;

namespace VisualExceptions
{
	class ExceptionInfo
	{
		delegate string GetClassNameDelegate(Exception instance);
		static readonly MethodInfo m_GetClassName = Method(typeof(Exception), "GetClassName");
		static readonly GetClassNameDelegate GetClassName = MethodDelegate<GetClassNameDelegate>(m_GetClassName);

		delegate string ExceptionToStringDelegate(Exception instance, bool needFileLineInfo, bool needMessage);
		static readonly MethodInfo m_ToString = Method(typeof(Exception), "ToString", [typeof(bool), typeof(bool)]);
		static readonly ExceptionToStringDelegate ExceptionToString = MethodDelegate<ExceptionToStringDelegate>(m_ToString);

		delegate string ExtractHarmonyEnhancedStackTraceDelegate(StackTrace trace, bool forceRefresh, out int hashRef);
		static readonly MethodInfo m_ExtractHarmonyEnhancedStackTrace = Method("HarmonyMod.ExceptionTools:ExtractHarmonyEnhancedStackTrace", [typeof(StackTrace), typeof(bool), typeof(int).MakeByRefType()]);
		static readonly ExtractHarmonyEnhancedStackTraceDelegate ExtractHarmonyEnhancedStackTrace = MethodDelegate<ExtractHarmonyEnhancedStackTraceDelegate>(m_ExtractHarmonyEnhancedStackTrace);

		readonly Exception exception;
		private StackTrace trace;
		private string stackTrace;
		private int hash;

		ExceptionDetails details = null;

		internal ExceptionInfo(Exception exception)
		{
			this.exception = exception;
		}

		internal void Analyze()
		{
			if (hash == 0)
			{
				trace = new StackTrace(exception);
				stackTrace = ExtractHarmonyEnhancedStackTrace(trace, true, out hash);
			}
		}

		internal ExceptionDetails GetReport()
		{
			if (details == null)
			{
				details = new ExceptionDetails()
				{
					exceptionMessage = GetMessage(exception),
					mods = []
				};
				Assembly lastAssembly = null;
				var previousMethods = new HashSet<MethodBase>();
				foreach (var modInfo in GetAllMethods(exception, 0))
				{
					var method = modInfo.method;
					var metaData = modInfo.metaData;
					if (previousMethods.Add(method))
					{
						details.topMethod ??= method.ShortDescription();
						var assembly = method.DeclaringType.Assembly;
						if (assembly != Tools.HarmonyModAssembly)
						{
							if (assembly != lastAssembly)
								details.mods.Add(new ExceptionDetails.Mod(method, metaData));
							else
								details.mods.Last().methods.Add(method);
							lastAssembly = assembly;
						}
					}
				}
			}
			return details;
		}

		internal string GetStacktrace()
		{
			Analyze();
			var sb = new StringBuilder();
			_ = sb.Append($"Exception");
			if (trace != null && trace.FrameCount > 0)
			{
				var frame = trace.GetFrame(trace.FrameCount - 1);
				var method = Harmony.GetOriginalMethodFromStackframe(frame);
				if (method != null)
					_ = sb.Append($" in {method.DeclaringType.FullName}.{method.Name}");
			}
			_ = sb.Append($": {GetClassName(exception)}");

			var message = exception.Message;
			if (message != null && message.Length > 0)
				_ = sb.Append($": {message}");

			if (exception.InnerException != null)
			{
				var txt = ExceptionToString(exception.InnerException, true, true);
				_ = sb.Append($" ---> {txt}\n   --- End of inner exception stack trace ---");
			}

			if (stackTrace != null)
				_ = sb.Append($"\n{stackTrace}");

			return sb.ToString();
		}

		internal void Remove()
		{
			ExceptionState.Remove(this);
			if (ExceptionState.configuration.UseSound)
				SoundDefOf.TabOpen.PlayOneShotOnCamera(null);
		}

		internal void Ban()
		{
			ExceptionState.Ban(this);
			Remove();
		}

		string GetMessage(Exception ex)
		{
			Analyze();
			var str = $"[Ref {hash:X}] {ex.GetType().FullName}";
			var msg = ex.Message;
			if (msg.NullOrEmpty() == false)
				str += ": " + msg.Trim();
			if (ex.InnerException != null)
				str += " ---> " + GetMessage(ex.InnerException);
			return str;
		}

		List<ModInfo> GetAllMethods(Exception ex, int level)
		{
			var modInfos = new List<ModInfo>();
			var inner = ex.InnerException;
			if (inner != null)
				modInfos.AddRange(GetAllMethods(inner, level + 1));

			var trace = new StackTrace(ex, 0, true);
			foreach (var frame in trace.GetFrames())
			{
				var method = Harmony.GetMethodFromStackframe(frame);
				var patches = Mods.FindPatches(method);

				if (ExceptionState.configuration.IncludeFinalizers)
					modInfos.AddRange(Mods.GetFinalizers(patches));
				modInfos.AddRange(Mods.GetPostfixes(patches));
				modInfos.AddRange(Mods.GetPrefixes(patches));
				modInfos.AddRange(Mods.GetTranspilers(patches));

				var metaData = Mods.GetMetadataIfMod(method);
				if (metaData != null)
					modInfos.Add(new ModInfo { method = method, metaData = metaData });
			}
			return modInfos;
		}

		internal class Comparer : IEqualityComparer<ExceptionInfo>
		{
			public bool Equals(ExceptionInfo x, ExceptionInfo y) => x.GetHashCode() == y.GetHashCode();
			public int GetHashCode(ExceptionInfo obj) => obj.GetHashCode();
		}

		public override int GetHashCode()
		{
			Analyze();
			return hash;
		}
	}
}
