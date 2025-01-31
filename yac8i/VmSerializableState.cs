using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Linq;
using System;

namespace yac8i
{
    [XmlRoot("state")]
    public class VmSerializableState
    {
        public int InstructionsPerFrame = 7;
        public int ProgramBytesCount = 0;
        public ushort IRegister;
        public ushort ProgramCounter;
        public int InstructionsToExecuteInFrame = 0;
        public byte SoundTimer = 0;
        public bool BeepStatus = false;
        public byte DelayTimer = 0;
        [XmlElement(DataType = "hexBinary")]
        public byte[] LoadedProgram = new byte[4096];
        [XmlElement(DataType = "hexBinary")]
        public byte[] Memory = new byte[4096];
        [XmlElement(DataType = "hexBinary")]
        public byte[] Registers = new byte[16];

        [XmlElement("stack")]
        public string SerializableStack
        {
            get
            {
                return string.Join(',', new List<ushort>(Stack));
            }
            set
            {
                foreach (var v in value.Split(',').Reverse())
                {
                    if (ushort.TryParse(v, out var val))
                    {
                        Stack.Push(val);
                    }
                }
            }
        }

        [XmlElement("surface")]
        public string SerializableSurface
        {


            get
            {
                StringBuilder sb = new();
                for (int j = 0; j < 32; j++)
                {
                    for (int i = 0; i < 64; i++)
                    {
                        sb.Append(Surface[i, j] ? "1" : "0");
                    }
                }
                return sb.ToString();
            }
            set
            {
                for (int i = 0; i < 64; i++)
                {
                    for (int j = 0; j < 32; j++)
                    {
                        Surface[i, j] = value[j * 64 + i] == '1';
                    }
                }

            }
        }

        [XmlIgnore]
        public bool[,] Surface = new bool[64, 32];
        [XmlIgnore]
        public Stack<ushort> Stack = new();


        public static void Store(string fileName, VmSerializableState state)
        {
            XmlSerializer serializer = new(typeof(VmSerializableState));
            XmlWriterSettings settings = new() { Indent = true };
            using var writer = XmlWriter.Create(fileName, settings);
            serializer.Serialize(writer, state);
        }

        public static void Restore(string fileName, out VmSerializableState state)
        {
            XmlSerializer serializer = new(typeof(VmSerializableState));
            using var reader = XmlReader.Create(fileName);
            state = (VmSerializableState)serializer.Deserialize(reader);
        }
    }
}
