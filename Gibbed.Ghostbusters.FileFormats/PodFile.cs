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
        public UInt32 CompressedSize;
        public UInt32 Offset;
        public UInt32 UncompressedSize;
        public UInt32 CompressionLevel;
        public UInt32 Timestamp;
        public UInt32 Checksum;

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
            public UInt32 Checksum;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x50)]
            public string Comment;
            public Int32 IndexCount;
            public UInt32 Unknown05C;
            public UInt32 Unknown060;
            public UInt32 Unknown064;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x50)]
            public string Author;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x50)]
            public string Copyright;
            public UInt32 IndexOffset;
            public UInt32 Unknown10C;
            public UInt32 NamesSize;
            public UInt32 Unknown114;
            public UInt32 Unknown118;
            public UInt32 Unknown11C;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x50)]
            public string Unknown120;
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
            string magic = input.ReadStringASCII(4);

            if (magic != "POD3" && magic != "POD4" && magic != "POD5")
            {
                throw new InvalidOperationException("only pod versions 3 to 5 are supported");
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

                nameIndexes[i] = input.ReadValueU32();
                entry.CompressedSize = input.ReadValueU32();
                entry.Offset = input.ReadValueU32();

                if (this.Version >= 4)
                {
                    entry.UncompressedSize = input.ReadValueU32();
                    entry.CompressionLevel = input.ReadValueU32();
                }
                else
                {
                    entry.UncompressedSize = entry.CompressedSize;
                    entry.CompressionLevel = 0;
                }

                entry.Timestamp = input.ReadValueU32();
                entry.Checksum = input.ReadValueU32();

                this.Entries.Add(entry);
            }
            
            byte[] names = new byte[header.NamesSize];
            input.Read(names, 0, names.Length);

            for (int i = 0; i < header.IndexCount; i++)
            {
                PodEntry entry = this.Entries[i];

                entry.Name = names.ToStringASCIIZ(nameIndexes[i]);

                if (entry.CompressedSize != entry.UncompressedSize && entry.CompressionLevel == 0)
                {
                    throw new FormatException("compressed and uncompressed size mismatch when compression level is zero");
                }
            }
        }
    }
}
