using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmdArtPlayerServer
{
    class Debug
    {
        public static void Log(string str)
        {
            Console.WriteLine(str);
        }

        public static void LogError(string str)
        {
            Console.WriteLine("Error: " + str);
        }
    }
}
