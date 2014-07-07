/* Copyright (c) 2014 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Gibbed.IO;

namespace Gibbed.Ghostbusters.FileFormats
{
    public class PodFile
    {
        private const uint _Signature = 0x504F44;

        #region Fields
        private Endian _Endian;
        private byte _Version;
        private uint _Checksum;
        private string _Comment;
        private uint _Unknown05C;
        private uint _Unknown060 = 1000;
        private uint _Unknown064 = 1000;
        private string _Author;
        private string _Copyright;
        private uint _Unknown10C;
        private uint _Unknown114;
        private int _Unknown118 = -1;
        private int _Unknown11C = -1;
        private string _NextName;
        private readonly List<Entry> _Entries;
        #endregion

        public PodFile()
        {
            this._Entries = new List<Entry>();
        }

        #region Properties
        public Endian Endian
        {
            get { return this._Endian; }
            set { this._Endian = value; }
        }

        public byte Version
        {
            get { return this._Version; }
            set { this._Version = value; }
        }

        public uint Checksum
        {
            get { return this._Checksum; }
            set { this._Checksum = value; }
        }

        public string Comment
        {
            get { return this._Comment; }
            set { this._Comment = value; }
        }

        public uint Unknown05C
        {
            get { return this._Unknown05C; }
            set { this._Unknown05C = value; }
        }

        public uint Unknown060
        {
            get { return this._Unknown060; }
            set { this._Unknown060 = value; }
        }

        public uint Unknown064
        {
            get { return this._Unknown064; }
            set { this._Unknown064 = value; }
        }

        public string Author
        {
            get { return this._Author; }
            set { this._Author = value; }
        }

        public string Copyright
        {
            get { return this._Copyright; }
            set { this._Copyright = value; }
        }

        public uint Unknown10C
        {
            get { return this._Unknown10C; }
            set { this._Unknown10C = value; }
        }

        public uint Unknown114
        {
            get { return this._Unknown114; }
            set { this._Unknown114 = value; }
        }

        public int Unknown118
        {
            get { return this._Unknown118; }
            set { this._Unknown118 = value; }
        }

        public int Unknown11C
        {
            get { return this._Unknown11C; }
            set { this._Unknown11C = value; }
        }

        public string NextName
        {
            get { return this._NextName; }
            set { this._NextName = value; }
        }

        public List<Entry> Entries
        {
            get { return this._Entries; }
        }

        public int HeaderSize
        {
            get { return this._Version >= 5 ? 368 : 288; }
        }
        #endregion

        public void SerializeHeader(Stream output, long indexOffset, uint indexCount, uint stringSize)
        {
            var endian = this._Endian;
            var version = this._Version;

            var magic = _Signature << 8;
            magic |= 0x30u + this._Version;

            output.WriteValueU32(magic, Endian.Big);
            output.WriteValueU32(this._Checksum, endian);
            output.WriteString(this._Comment, 80, Encoding.ASCII);
            output.WriteValueU32(indexCount, endian);
            output.WriteValueU32(this._Unknown05C, endian);
            output.WriteValueU32(this._Unknown060, endian);
            output.WriteValueU32(this._Unknown064, endian);
            output.WriteString(this._Author, 80, Encoding.ASCII);
            output.WriteString(this._Copyright, 80, Encoding.ASCII);
            output.WriteValueU32((uint)indexOffset, endian);
            output.WriteValueU32(this._Unknown10C, endian);
            output.WriteValueU32(stringSize, endian);
            output.WriteValueU32(this._Unknown114, endian);
            output.WriteValueS32(this._Unknown118, endian);
            output.WriteValueS32(this._Unknown11C, endian);

            if (version >= 5)
            {
                output.WriteString(this._NextName, 80, Encoding.ASCII);
            }
        }

        public void SerializeIndex(Stream output, out long indexOffset, out uint indexCount, out uint stringSize)
        {
            using (var stringBuffer = new MemoryStream())
            {
                var endian = this._Endian;
                var version = this._Version;
                var writer = new StringTableWriter(stringBuffer, Encoding.ASCII);

                indexOffset = (uint)output.Position;
                indexCount = (uint)this._Entries.Count;

                foreach (var entry in this._Entries)
                {
                    var nameOffset = writer.Put(entry.Name);
                    output.WriteValueU32(nameOffset, endian);
                    output.WriteValueU32(entry.CompressedSize, endian);
                    output.WriteValueU32(entry.Offset, endian);

                    if (version >= 4)
                    {
                        output.WriteValueU32(entry.UncompressedSize, endian);
                        output.WriteValueU32(entry.CompressionLevel, endian);
                    }

                    output.WriteValueU32(entry.Timestamp, endian);
                    output.WriteValueU32(entry.Checksum, endian);
                }

                stringBuffer.Flush();
                stringSize = (uint)stringBuffer.Length;
                stringBuffer.Position = 0;
                output.WriteFromStream(stringBuffer, stringSize);
            }
        }

        public void Deserialize(Stream input)
        {
            long basePosition = input.Position;

            var magic = input.ReadValueU32(Endian.Big);
            var version = (byte)((magic & 0xFFu) - 0x30u);
            magic >>= 8;

            if (magic != _Signature)
            {
                throw new FormatException("not a pod file");
            }

            if (version < 3 || version > 5)
            {
                throw new InvalidOperationException("only versions 3 to 5 are supported");
            }

            var endian = Endian.Little;

            var checksum = input.ReadValueU32(endian);
            var comment = input.ReadString(80, true, Encoding.ASCII);
            var indexCount = input.ReadValueS32(endian);
            var unknown05C = input.ReadValueU32(endian);
            var unknown060 = input.ReadValueU32(endian);
            var unknown064 = input.ReadValueU32(endian);
            var author = input.ReadString(80, true, Encoding.ASCII);
            var copyright = input.ReadString(80, true, Encoding.ASCII);
            var indexOffset = input.ReadValueU32(endian);
            var unknown10C = input.ReadValueU32(endian);
            var stringSize = input.ReadValueU32(endian);
            var unknown114 = input.ReadValueU32(endian);
            var unknown118 = input.ReadValueS32(endian);
            var unknown11C = input.ReadValueS32(endian);
            var nextName = version < 5 ? string.Empty : input.ReadString(80, true, Encoding.ASCII);

            if (indexCount < 0 || indexCount > 9999999)
            {
                throw new FormatException("bad index count");
            }

            input.Seek(basePosition + indexOffset, SeekOrigin.Begin);
            var nameOffsets = new uint[indexCount];
            var entries = new List<Entry>();
            for (int i = 0; i < indexCount; i++)
            {
                nameOffsets[i] = input.ReadValueU32(endian);

                var entry = new Entry();
                entry.CompressedSize = input.ReadValueU32(endian);
                entry.Offset = input.ReadValueU32(endian);

                if (version >= 4)
                {
                    entry.UncompressedSize = input.ReadValueU32(endian);
                    entry.CompressionLevel = input.ReadValueU32(endian);
                }
                else
                {
                    entry.UncompressedSize = entry.CompressedSize;
                    entry.CompressionLevel = 0;
                }

                entry.Timestamp = input.ReadValueU32(endian);
                entry.Checksum = input.ReadValueU32(endian);

                entries.Add(entry);

                if (entry.CompressedSize != entry.UncompressedSize && entry.CompressionLevel == 0)
                {
                    throw new FormatException("compressed and uncompressed size mismatch when compression level is zero");
                }
            }

            using (var stringBuffer = input.ReadToMemoryStream(stringSize))
            {
                for (int i = 0; i < indexCount; i++)
                {
                    var offset = nameOffsets[i];
                    stringBuffer.Position = offset;
                    entries[i].Name = stringBuffer.ReadStringZ(Encoding.ASCII);
                }
            }

            if ((unknown05C != 0 && unknown05C != indexCount) ||
                unknown060 != 1000 ||
                unknown064 != 1000 ||
                /*unknown10C != 0 ||*/
                unknown114 != 0 //||
                /*unknown118 != -1 ||*/
                /*unknown11C != -1*/)
            {
                throw new FormatException();
            }

            this._Version = version;
            this._Checksum = checksum;
            this._Comment = comment;
            this._Unknown05C = unknown05C;
            this._Unknown060 = unknown060;
            this._Unknown064 = unknown064;
            this._Author = author;
            this._Copyright = copyright;
            this._Unknown10C = unknown10C;
            this._Unknown114 = unknown114;
            this._Unknown118 = unknown118;
            this._Unknown11C = unknown11C;
            this._NextName = nextName;
            this._Entries.Clear();
            this._Entries.AddRange(entries);
        }

        public class Entry
        {
            public string Name;
            public uint CompressedSize;
            public uint Offset;
            public uint UncompressedSize;
            public uint CompressionLevel;
            public uint Timestamp;
            public uint Checksum;

            public override string ToString()
            {
                return this.Name;
            }
        }

        private class StringTableWriter
        {
            public StringTableWriter(Stream output, Encoding encoding)
            {
                this._Output = output;
                this._Encoding = encoding;
            }

            private readonly Stream _Output;
            private readonly Encoding _Encoding;
            private readonly Dictionary<string, uint> _Offsets = new Dictionary<string, uint>();

            public uint Put(string value)
            {
                if (this._Offsets.ContainsKey(value) == true)
                {
                    return this._Offsets[value];
                }

                uint offset = (uint)this._Output.Position;
                this._Output.WriteStringZ(value, this._Encoding);
                this._Offsets.Add(value, offset);
                return offset;
            }
        }
    }
}
