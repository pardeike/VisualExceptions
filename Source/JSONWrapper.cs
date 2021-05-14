using System;
using UnityEngine;

namespace VisualExceptions
{
	internal static class JsonHelper
	{
		public static T[] FromJson<T>(string json) => JsonUtility.FromJson<Wrapper<T>>(json).Items;
		public static string ToJson<T>(T[] array) => JsonUtility.ToJson(new Wrapper<T> { Items = array });

		[Serializable]
		private class Wrapper<T>
		{
			public T[] Items;
		}
	}
}
