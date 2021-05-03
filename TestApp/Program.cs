using System;
using System.IO;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var data =EsprimaToJs.Conversion.ToJs(File.ReadAllText("test.js"));
            File.WriteAllText("jquery.js", data);
            Console.WriteLine(data);
            Console.Read();
        }
    }
}
