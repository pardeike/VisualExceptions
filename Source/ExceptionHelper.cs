using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace VisualExceptions
{
	internal static class ExceptionHelper
	{
		internal static readonly AccessTools.FieldRef<StackTrace, StackTrace[]> captured_traces_ref = AccessTools.FieldRefAccess<StackTrace, StackTrace[]>("captured_traces");
		internal static readonly AccessTools.FieldRef<StackFrame, string> internalMethodName_ref = AccessTools.FieldRefAccess<StackFrame, string>("internalMethodName");
		internal static readonly AccessTools.FieldRef<StackFrame, long> methodAddress_ref = AccessTools.FieldRefAccess<StackFrame, long>("methodAddress");

		internal delegate void GetFullNameForStackTrace(StackTrace instance, StringBuilder sb, MethodBase mi);
		internal static readonly MethodInfo m_GetFullNameForStackTrace = AccessTools.Method(typeof(StackTrace), "GetFullNameForStackTrace");
		internal static readonly GetFullNameForStackTrace getFullNameForStackTrace = AccessTools.MethodDelegate<GetFullNameForStackTrace>(m_GetFullNameForStackTrace);

		internal delegate uint GetMethodIndex(StackFrame instance);
		internal static readonly MethodInfo m_GetMethodIndex = AccessTools.Method(typeof(StackFrame), "GetMethodIndex");
		internal static readonly GetMethodIndex getMethodIndex = AccessTools.MethodDelegate<GetMethodIndex>(m_GetMethodIndex);

		internal delegate string GetSecureFileName(StackFrame instance);
		internal static readonly MethodInfo m_GetSecureFileName = AccessTools.Method(typeof(StackFrame), "GetSecureFileName");
		internal static readonly GetSecureFileName getSecureFileName = AccessTools.MethodDelegate<GetSecureFileName>(m_GetSecureFileName);

		internal delegate string GetAotId();
		internal static readonly MethodInfo m_GetAotId = AccessTools.Method(typeof(StackTrace), "GetAotId");
		internal static readonly GetAotId getAotId = AccessTools.MethodDelegate<GetAotId>(m_GetAotId);

		internal delegate string GetClassName(Exception instance);
		internal static readonly MethodInfo m_GetClassName = AccessTools.Method(typeof(Exception), "GetClassName");
		internal static readonly GetClassName getClassName = AccessTools.MethodDelegate<GetClassName>(m_GetClassName);

		internal delegate string ToStringBoolBool(Exception instance, bool needFileLineInfo, bool needMessage);
		internal static readonly MethodInfo m_ToString = AccessTools.Method(typeof(Exception), "ToString", [typeof(bool), typeof(bool)]);
		internal static readonly ToStringBoolBool toString = AccessTools.MethodDelegate<ToStringBoolBool>(m_ToString);

		internal static MethodBase GetExpandedMethod(this StackFrame frame, out Patches patches)
		{
			patches = new Patches([], [], [], []);
			var method = Harmony.GetMethodFromStackframe(frame);
			if (method != null && method is MethodInfo replacement)
			{
				var original = Harmony.GetOriginalMethod(replacement);
				if (original != null)
				{
					method = original;
					patches = Harmony.GetPatchInfo(method);
				}
			}
			return method;
		}

		internal static bool AddHarmonyFrames(this StackTrace trace, StringBuilder sb)
		{
			if (trace.FrameCount == 0) return false;
			for (var i = 0; i < trace.FrameCount; i++)
			{
				var frame = trace.GetFrame(i);
				if (i > 0) _ = sb.Append('\n');
				_ = sb.Append("  at ");

				var method = frame.GetExpandedMethod(out var patches);

				if (method == null)
				{
					var internalMethodName = internalMethodName_ref(frame);
					if (internalMethodName != null)
						_ = sb.Append(internalMethodName);
					else
						_ = sb.AppendFormat("<0x{0:x5} + 0x{1:x5}> <unknown method>", methodAddress_ref(frame), frame.GetNativeOffset());
				}
				else
				{
					getFullNameForStackTrace(trace, sb, method);
					if (frame.GetILOffset() == -1)
					{
						_ = sb.AppendFormat(" <0x{0:x5} + 0x{1:x5}>", methodAddress_ref(frame), frame.GetNativeOffset());
						if (getMethodIndex(frame) != 16777215U)
							_ = sb.AppendFormat(" {0}", getMethodIndex(frame));
					}
					else
						_ = sb.AppendFormat(" [0x{0:x5}]", frame.GetILOffset());

					var fileName = getSecureFileName(frame);
					if (fileName[0] == '<')
					{
						var versionId = method.Module.ModuleVersionId.ToString("N");
						var aotId = getAotId();
						if (frame.GetILOffset() != -1 || aotId == null)
							fileName = string.Format("<{0}>", versionId);
						else
							fileName = string.Format("<{0}#{1}>", versionId, aotId);
					}
					_ = sb.AppendFormat(" in {0}:{1} ", fileName, frame.GetFileLineNumber());

					void AppendPatch(IEnumerable<Patch> fixes, string name)
					{
						foreach (var patch in PatchProcessor.GetSortedPatchMethods(method, fixes.ToArray()))
						{
							var owner = fixes.First(p => p.PatchMethod == patch).owner;
							var parameters = patch.GetParameters().Join(p => $"{p.ParameterType.Name} {p.Name}");
							_ = sb.AppendFormat("\n     - {0} {1}: {2} {3}:{4}({5})", name, owner, patch.ReturnType.Name, patch.DeclaringType.FullName, patch.Name, parameters);
						}
					}
					AppendPatch(patches.Transpilers, "transpiler");
					AppendPatch(patches.Prefixes, "prefix");
					AppendPatch(patches.Postfixes, "postfix");
					AppendPatch(patches.Finalizers, "finalizer");
				}
			}
			return true;
		}

		public static string WithHarmonyString(this StackTrace trace)
		{
			var sb = new StringBuilder();
			if (captured_traces_ref(trace) != null)
			{
				var array = captured_traces_ref(trace);
				for (int i = 0; i < array.Length; i++)
				{
					if (array[i].AddHarmonyFrames(sb))
						_ = sb.Append("\n--- End of stack trace from previous location where exception was thrown ---\n");
				}
			}
			_ = trace.AddHarmonyFrames(sb);
			return sb.ToString();
		}
	}
}
