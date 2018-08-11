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
            // Run example
            Basic_Usage.Run().GetAwaiter().GetResult();

            // Wait for keypress
            Console.ReadKey();
        }
    }
}
