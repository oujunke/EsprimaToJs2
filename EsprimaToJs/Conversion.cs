using Esprima.Ast;
using System;
using System.Collections.Generic;
using System.Text;

namespace EsprimaToJs
{
    public class Conversion
    {
        public static string ToJs(Script script)
        {
            return ToJs(script.Body);
        }
        public static string ToJs(IEnumerable<Node> script)
        {
            System.IO.MemoryStream memoryStream = new System.IO.MemoryStream();
            var nodeConv = new NodeConv(memoryStream);
            foreach (var item in script)
            {
                nodeConv.WriteNode(item);
                //nodeConv.NewBranch();
                nodeConv.NewLine();
            }
            return Encoding.UTF8.GetString(memoryStream.ToArray());
        }
        public static string ToJs(string js)
        {
            Esprima.JavaScriptParser scriptParser = new Esprima.JavaScriptParser(js, new Esprima.ParserOptions(js)
            {
                AdaptRegexp = true,
                Tolerant = true,
                //Tokens = true,
                //Comment = true,
            });
            return ToJs(scriptParser.ParseScript());
        }
    }
}
