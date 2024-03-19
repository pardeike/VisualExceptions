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

namespace VisualExceptions
{
	class ExceptionInfo
	{
		int hash = 0;
		internal readonly Exception exception;
		ExceptionDetails details = null;

		internal ExceptionInfo(Exception exception)
		{
			this.exception = exception;
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
			var sb = new StringBuilder();
			_ = sb.Append("Exception");
			var trace = new StackTrace(exception);
			if (trace != null && trace.FrameCount > 0)
			{
				var frame = trace.GetFrame(trace.FrameCount - 1);
				var method = frame.GetExpandedMethod(out _);
				if (method != null)
					_ = sb.Append($" in {method.DeclaringType.FullName}.{method.Name}");
			}
			_ = sb.Append($": {ExceptionHelper.getClassName(exception)}");

			var message = exception.Message;
			if (message != null && message.Length > 0)
				_ = sb.Append($": {message}");

			if (exception.InnerException != null)
			{
				var txt = ExceptionHelper.toString(exception.InnerException, true, true);
				_ = sb.Append($" ---> {txt}\n   --- End of inner exception stack trace ---");
			}

			var stackTrace = trace.WithHarmonyString();
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
			var str = ex.GetType().FullName;
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
			if (hash == 0)
				hash = GetStacktrace().GetHashCode();
			return hash;
		}
	}
}
