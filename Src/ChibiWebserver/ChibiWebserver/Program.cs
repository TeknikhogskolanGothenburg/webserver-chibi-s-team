using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChibiWebserver
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] prefixes = new string[] { "http://127.0.0.1:8080/", "http://localhost:8080/" };

            // Make webserver and start it
            WebServer webserver = new WebServer(prefixes);
            webserver.Start();

            // Ask when to stop, and wait for it
            Console.WriteLine("Simply press a key to shutdown webserver.");
            Console.ReadKey();

            // Stop webserver
            webserver.Stop();
            Console.ReadKey();
        }
    }
}
