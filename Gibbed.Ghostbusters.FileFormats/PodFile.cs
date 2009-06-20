using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Gibbed.Helpers;

namespace Gibbed.Ghostbusters.FileFormats
{
    public class PodEntry
    {
        public string Name;
        public UInt32 UncompressedSize;
        public UInt32 Offset;
        public UInt32 CompressedSize;
        public UInt32 Unknown5;
        public UInt64 Hash;

        public override string ToString()
        {
            return this.Name;
        }
    }

    public class PodFile
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x170, CharSet = CharSet.Ansi)]
        internal struct Header
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
            public string Magic;
            public UInt32 Unknown004;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x50)]
            public string Unknown008;
            public Int32 IndexCount;
            public UInt32 Unknown05C;
            public UInt32 Unknown060;
            public UInt32 Unknown064;
            
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x50)]
            public string Unknown068;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x50)]
            public string Unknown0B8;
            public UInt32 IndexOffset;
            public UInt32 Unknown10C;
            public UInt32 NamesSize;
            public UInt32 Unknown114;
        }

        public byte Version;
        public List<PodEntry> Entries;

        public void Serialize(Stream output)
        {
            throw new NotImplementedException();
        }

        public void Deserialize(Stream input)
        {
            long position = input.Position;
            string magic = input.ReadASCII(4);

            if (magic != "POD3" && magic != "POD4" && magic != "POD5")
            {
                throw new Exception();
            }

            input.Seek(position, SeekOrigin.Begin);
            Header header;

            if (magic == "POD3")
            {
                this.Version = 3;
                header = input.ReadStructure<Header>(0x120);
            }
            else if (magic == "POD4")
            {
                this.Version = 4;
                header = input.ReadStructure<Header>(0x120);
            }
            else //if (magic == "POD5")
            {
                this.Version = 5;
                header = input.ReadStructure<Header>(0x170);
            }

            if (header.IndexCount < 0 || header.IndexCount > 9999999)
            {
                throw new InvalidOperationException("bad index count");
            }

            input.Seek(header.IndexOffset, SeekOrigin.Begin);
            
            this.Entries = new List<PodEntry>();
            UInt32[] nameIndexes = new UInt32[header.IndexCount];

            for (int i = 0; i < header.IndexCount; i++)
            {
                PodEntry entry = new PodEntry();

                nameIndexes[i] = input.ReadU32();
                entry.UncompressedSize = input.ReadU32();
                entry.Offset = input.ReadU32();

                if (this.Version >= 4)
                {
                    entry.CompressedSize = input.ReadU32();
                    entry.Unknown5 = input.ReadU32();
                }
                else
                {
                    entry.CompressedSize = entry.UncompressedSize;
                    entry.Unknown5 = 0;
                }

                entry.Hash = input.ReadU64();

                this.Entries.Add(entry);
            }
            
            byte[] names = new byte[header.NamesSize];
            input.Read(names, 0, names.Length);

            for (int i = 0; i < header.IndexCount; i++)
            {
                this.Entries[i].Name = names.ReadASCIIZ(nameIndexes[i]);

                if (this.Entries[i].CompressedSize != this.Entries[i].UncompressedSize)
                {
                    throw new Exception();
                }

                if (this.Entries[i].Unknown5 != 0)
                {
                    throw new Exception();
                }
            }
        }
    }
}
