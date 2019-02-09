using System;

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
