using System;
using System.Runtime;
using System.Runtime.InteropServices;
using GetSomeInput;
using PerformanceStatistics;

namespace Test
{
    public static class Program
    {
        private static bool _RunForever = true;

        public static void Main(string[] args)
        {
            while (_RunForever)
            {
                string userInput = Inputty.GetString("Command [?/help]:", null, false);

                switch (userInput)
                {
                    case "q":
                        _RunForever = false;
                        break;
                    case "?":
                        Menu();
                        break;
                    case "cls":
                        Console.Clear();
                        break;
                    case "stats":
                        DisplayStats();
                        break;
                    case "port":
                        CountByPort();
                        break;
                }
            }
        }

        private static void Menu()
        {
            Console.WriteLine("");
            Console.WriteLine("Available Commands");
            Console.WriteLine("   q               Quit this program");
            Console.WriteLine("   ?               Help, this menu");
            Console.WriteLine("   cls             Clear the screen");
            Console.WriteLine("   stats           Gather current statistics");
            Console.WriteLine("   port            Get connection count by port");
            Console.WriteLine("");
        }

        private static void DisplayStats()
        {
            WindowsPerformanceStatistics stats = new WindowsPerformanceStatistics();
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine("");
                Console.WriteLine(stats.ToString());
                Console.WriteLine("");
            }
            else
            {
                Console.WriteLine("");
                Console.WriteLine("Unsupported platform");
                Console.WriteLine("");
            }
        }

        private static void CountByPort()
        {
            string srcPortStr = Inputty.GetString("Source port :", null, true);
            string dstPortStr = Inputty.GetString("Dest port   :", null, true);

            int? srcPort = null;
            int? dstPort = null;
            if (!String.IsNullOrEmpty(srcPortStr)) srcPort = Convert.ToInt32(srcPortStr);
            if (!String.IsNullOrEmpty(dstPortStr)) dstPort = Convert.ToInt32(dstPortStr);

            WindowsPerformanceStatistics stats = new WindowsPerformanceStatistics();
            Console.WriteLine(stats.GetActiveTcpConnections(srcPort, dstPort).Length + " connections");
        }
    }
}