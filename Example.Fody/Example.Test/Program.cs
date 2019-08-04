using Example.Fody;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Console;
namespace Example.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            MethodDecorator.InitFun = (s, e, arg) =>
            {
                if (e == null)
                {
                    return;
                }
                WriteLine($"On Init {e?.Name} with params ");
                int count = 0;
                foreach (var item in arg)
                {
                    WriteLine($"Param {count++}: {item}");
                }
            };
            MethodDecorator.OnEntryFuc = () =>
            {
                WriteLine($"On Entry");
            };
            Print("Sample message");
            ReadLine();
        }

        private static int Add(int a, int b, int c)
        {
            return a + b;
        }

        public static void Print(string msg)
        {
            WriteLine(msg);
        }

        public void Test()
        {
            try
            {
                Print("");
            }
            catch (Exception)
            {
                MethodDecorator.Instance.OnExit();
                throw;
            }
        }
    }


}
