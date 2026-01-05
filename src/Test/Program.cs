using System;
using System.Runtime;
using System.Runtime.InteropServices;
using GetSomeInput;
using PerformanceStatistics;
using PerformanceStatistics.Windows;

namespace Test
{
    public static class Program
    {
        private static bool _RunForever = true;
        private static WindowsPerformanceStatistics _Statistics = new WindowsPerformanceStatistics();

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
                    case "json":
                        DisplayJson();
                        break;
                    case "port":
                        CountByPort();
                        break;
                    case "add":
                        AddProcess();
                        break;
                    case "del":
                        RemoveProcess();
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
            Console.WriteLine("   json            Gather current statistics in JSON");
            Console.WriteLine("   port            Get connection count by port");
            Console.WriteLine("   add             Add monitored process by name");
            Console.WriteLine("   del             Delete monitored process by name");
            Console.WriteLine("");
        }

        private static void DisplayStats()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine("");
                Console.WriteLine(_Statistics.ToString());
                Console.WriteLine("");

                Console.WriteLine("");
            }
            else
            {
                Console.WriteLine("");
                Console.WriteLine("Unsupported platform");
                Console.WriteLine("");
            }
        }

        private static void DisplayJson()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine("");
                Console.WriteLine(SerializationHelper.SerializeJson(_Statistics, true));
                Console.WriteLine("");

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

            Console.WriteLine(_Statistics.GetActiveTcpConnections(srcPort, dstPort).Length + " connections");
        }

        private static void AddProcess()
        {
            string name = Inputty.GetString("Name:", null, true);
            if (String.IsNullOrEmpty(name)) return;

            _Statistics.MonitoredProcessNames.Add(name);
        }

        private static void RemoveProcess()
        {
            string name = Inputty.GetString("Name:", null, true);
            if (String.IsNullOrEmpty(name)) return;

            if (_Statistics.MonitoredProcessNames.Contains(name))
                _Statistics.MonitoredProcessNames.Remove(name);
        }
    }
}