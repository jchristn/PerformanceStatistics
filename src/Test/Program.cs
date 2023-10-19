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
    }
}