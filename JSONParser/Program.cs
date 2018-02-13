using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JSONParser
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Count() < 1)
            {
                Console.WriteLine("You need to specify a file");
                Environment.Exit(0);
            }

            if (!File.Exists(args[0]))
            {
                Console.WriteLine($"File {args[0]} doesn't exist");
                Environment.Exit(0);
            }

            var @string = ReadJSON(File.OpenRead(args[0]));
            File.WriteAllText(Path.GetDirectoryName(args[0]) + "\\export.json", @string);
        }

        public static string ReadJSON(Stream input)
        {
            using (var br = new BinaryReaderX(input, true))
            {
                if (br.ReadString(4) != "PSLB")
                {
                    Console.WriteLine("This isn't a supported binary JSON");
                    Environment.Exit(0);
                }

                br.BaseStream.Position -= 4;
                return JsonConvert.SerializeObject(
                    new Init
                    {
                        magic = br.ReadString(4),
                        unk1 = br.ReadInt32(),
                        unk2 = br.ReadInt32(),
                        unk3 = br.ReadInt32(),
                        content = GetParameter(input)
                    },
                    Formatting.Indented);
            }
        }

        public static void WriteJSON()
        {

        }

        public static object GetParameter(Stream input)
        {
            using (var br = new BinaryReaderX(input, true))
            {
                var ident = br.ReadByte();
                switch (ident)
                {
                    case 1:
                        //string
                        var length = br.ReadInt32();
                        var str = br.ReadString(length);
                        return str;
                    case 2:
                        var integer = br.ReadInt32();
                        return integer;
                    case 3:
                        var flt = br.ReadSingle();
                        return flt;
                    case 4:
                        var @byte = br.ReadByte();
                        return @byte;
                    case 5:
                        //create new array and add
                        var count = br.ReadInt32();
                        var arr = new JArray();
                        for (int i = 0; i < count; i++)
                        {
                            length = br.ReadInt32();
                            str = br.ReadString(length);
                            var item = new Obj { name = str, @object = GetParameter(input) };
                            arr.Add(JObject.FromObject(item));
                        }
                        return arr;
                    case 6:
                        //declare array fields
                        count = br.ReadInt32();
                        var objArr = new List<object>();
                        for (int i = 0; i < count; i++)
                            objArr.Add(GetParameter(input));
                        return objArr;
                    default:
                        throw new Exception($"Unknown Identifier {ident} at 0x{br.BaseStream.Position - 1:X8}.");
                }
            }
        }

        public class Obj
        {
            public string name;
            public object @object;
        }

        public class Init
        {
            public string magic;
            public int unk1;
            public int unk2;
            public int unk3;

            public object content;
        }
    }
}
