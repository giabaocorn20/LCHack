using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using static LCHack.Plugin;


namespace LCHack
{
	public class Loader
	{
		private static GameObject Load;
		
		public static void Init()
		{
			Assembly currentAssembly = Assembly.GetExecutingAssembly();
			string[] resourceNames = currentAssembly.GetManifestResourceNames();

			foreach (string resourceName in resourceNames)
			{
				Console.WriteLine(resourceName);
			}
			Loader.Load = new UnityEngine.GameObject();
			Loader.Load.AddComponent<Hacks>();
			UnityEngine.Object.DontDestroyOnLoad(Loader.Load);

			AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

		}
		static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			return LoadAssem();
		}

		public static Assembly LoadAssem()
		{
			byte[] ba = null;
			string resource = "LCHack.0Harmony.dll";
			Assembly curAsm = Assembly.GetExecutingAssembly();
			using (Stream stm = curAsm.GetManifestResourceStream(resource))
			{
				ba = new byte[(int)stm.Length];
				stm.Read(ba, 0, (int)stm.Length);
				return Assembly.Load(ba);
			}
		}

		public static void Unload()
		{
			_Unload();
		}

		private static void _Unload()
		{
			GameObject.Destroy(Load);
			
		}

	}
}