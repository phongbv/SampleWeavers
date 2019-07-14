using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Console;
namespace Example.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            WriteLine(Add(2, 4,3));
            ReadLine();
        }

        private static int Add(int a, int b, int c)
        {
            return a + b;
        }
    }
}
