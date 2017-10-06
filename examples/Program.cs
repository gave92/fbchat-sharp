using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace examples
{
    class Program
    {
        static void Main(string[] args)
        {
            // Read email and pw from console
            Console.WriteLine("Insert Facebook email:");
            var email = Console.ReadLine();
            Console.WriteLine("Insert Facebook password:");
            var password = Console.ReadLine();

            // Run example
            new Basic_Usage().Run(email, password);

            // Wait for keypress
            Console.ReadKey();
        }
    }
}
