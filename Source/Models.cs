using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Verse;

namespace VisualExceptions
{
	[AttributeUsage(AttributeTargets.Property)]
	internal class SettingsAttribute : Attribute
	{
		internal readonly string label;

		internal SettingsAttribute(string label)
		{
			this.label = label;
		}
	}

	[DataContract]
	internal class Configuration
	{
		[DataMember]
		[Settings("Enabled")]
		internal bool Debugging { get; set; }

		[DataMember]
		[Settings("Show exceptions to the right")]
		internal bool TabToTheRight { get; set; }

		[DataMember]
		[Settings("Use sound")]
		internal bool UseSound { get; set; }

		[DataMember]
		[Settings("Include Finalizers")]
		internal bool IncludeFinalizers { get; set; }
	}

	[DataContract]
	internal class JSONMethods
	{
		[DataMember] internal JSONMethod[] Methods { get; set; }

		internal static JSONMethods Serialize(IEnumerable<MethodBase> methodBases)
		{
			return new JSONMethods() { Methods = methodBases.Select(mb => JSONMethod.Serialize(mb)).ToArray() };
		}

		internal IEnumerable<MethodBase> ToMethods()
		{
			return Methods.Select(m => m.ToMethod()).OfType<MethodBase>();
		}
	}

	[DataContract]
	internal class JSONMethod
	{
		[DataMember] internal string Type { get; set; }
		[DataMember] internal bool IsConstructor { get; set; }
		[DataMember] internal bool IsStatic { get; set; }
		[DataMember] internal string Name { get; set; }
		[DataMember] internal JSONParameter[] Parameters { get; set; }

		internal static JSONMethod Serialize(MethodBase method)
		{
			return new JSONMethod()
			{
				Type = method.DeclaringType.FullName,
				IsConstructor = method.IsConstructor,
				IsStatic = method.IsStatic,
				Name = method.Name,
				Parameters = method.GetParameters().Select(p => JSONParameter.Serialize(p)).ToArray()
			};
		}

		internal MethodBase ToMethod()
		{
			var type = AccessTools.TypeByName(Type);
			if (type == null) return null;
			var parameters = Parameters.Select(p => p.ToParameter()).ToArray();
			if (parameters.Any(p => p == null)) return null;
			if (IsConstructor) return AccessTools.Constructor(type, parameters, IsStatic);
			return AccessTools.Method(type, Name, parameters);
		}
	}

	[DataContract]
	internal class JSONParameter
	{
		[DataMember] internal string Type { get; set; }
		[DataMember] internal bool IsRef { get; set; }

		internal static JSONParameter Serialize(ParameterInfo parameter)
		{
			var basicType = parameter.ParameterType;
			if (basicType.IsByRef)
				basicType = basicType.GetElementType();
			return new JSONParameter()
			{
				Type = basicType.FullName,
				IsRef = parameter.ParameterType.IsByRef
			};
		}

		internal Type ToParameter()
		{
			var type = AccessTools.TypeByName(Type);
			if (IsRef)
				return type.MakeByRefType();
			return type;
		}
	}
}
