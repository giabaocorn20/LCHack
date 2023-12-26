using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LCHack
{
	public class Logger
	{
		public static void Log(string message)
		{
			System.IO.File.AppendAllText("log.txt", message + Environment.NewLine);
		}
	}
}
