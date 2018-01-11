using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChibiWebserver
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] prefixes = new string[2] { "http://127.0.0.1:8080/", "http://localhost:8080/" };

            WebServer webserver = new WebServer(prefixes);
            webserver.Run();
            Console.WriteLine("Simply press a key to shutdown webserver.");
            Console.ReadKey();
            webserver.Stop();
            Console.ReadKey();
        }
    }
}
