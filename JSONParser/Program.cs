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
            if (args.Count() < 2)
            {
                Console.WriteLine("Usage:\nJSONParser.exe <mode> <filepath>\n\nAvailable modes:\n" +
                    "-e\tExports a binary JSON to human-readable JSON\n-i\tImports a human-readable JSON into the binary form");
                Environment.Exit(0);
            }

            if (args[0] != "-e" && args[0] != "-i")
            {
                Console.WriteLine("Available modes:\n" +
                    "-e\tExports a binary JSON to human-readable JSON\n-i\tImports a human-readable JSON into the binary form");
                Environment.Exit(0);
            }

            if (!File.Exists(args[1]))
            {
                Console.WriteLine($"File {args[1]} doesn't exist");
                Environment.Exit(0);
            }

            if (args[0] == "-e")
            {
                var @string = ReadJSON(File.OpenRead(args[1]));
                File.WriteAllText(Path.GetDirectoryName(args[1]) + "\\export.json", @string);
            }
            else
            {
                WriteJSON(File.ReadAllText(args[1]), Path.GetDirectoryName(args[1]) + "\\import.json");
            }
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

        public static void WriteJSON(string json, string des)
        {
            using (var bw = new BinaryWriterX(File.OpenWrite(des)))
            {
                var parsed = JsonConvert.DeserializeObject<Init>(json);
                bw.WriteASCII(parsed.magic);
                bw.Write(parsed.unk1);
                bw.Write(parsed.unk2);
                bw.Write(parsed.unk3);
                WriteParameter(bw.BaseStream, parsed.content);
            }
        }

        public static void WriteParameter(Stream des, object input)
        {
            using (var bw = new BinaryWriterX(des, true))
            {
                switch (input)
                {
                    case JArray jarr:
                        try
                        {
                            jarr[0].ToObject(typeof(Obj));

                            //array consisting of Obj's
                            bw.Write((byte)5);
                            bw.Write(jarr.Count);

                            for (int i = 0; i < jarr.Count; i++)
                            {
                                var obj = jarr[i].ToObject(typeof(Obj));
                                bw.Write(Encoding.ASCII.GetByteCount((obj as Obj).name));
                                bw.WriteASCII((obj as Obj).name);
                                WriteParameter(bw.BaseStream, (obj as Obj).@object);
                            }
                        }
                        catch
                        {
                            //loose array
                            bw.Write((byte)6);
                            bw.Write(jarr.Count);
                            for (int i = 0; i < jarr.Count; i++)
                            {
                                WriteParameter(bw.BaseStream, jarr[i]);
                            }
                        }
                        break;
                    case string @string:
                        bw.Write((byte)1);
                        bw.Write(Encoding.ASCII.GetByteCount(@string));
                        bw.WriteASCII(@string);
                        break;
                    case float @float:
                        bw.Write((byte)3);
                        bw.Write(@float);
                        break;
                    case long @long:
                        if (@long < 0 || @long >= 256)
                        {
                            bw.Write((byte)2);
                            bw.Write((int)@long);
                        }
                        else
                        {
                            bw.Write((byte)4);
                            bw.Write((byte)@long);
                        }
                        break;
                    default:
                        ;
                        break;
                }
            }
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
