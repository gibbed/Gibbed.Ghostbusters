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
using System.Globalization;
using System.IO;
using System.Linq;
using Gibbed.Ghostbusters.FileFormats;
using Gibbed.IO;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using NDesk.Options;

namespace Gibbed.Ghostbusters.Pack
{
    internal class Program
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        private class MyFileEntry : PodFile.Entry
        {
            public string FilePath;
        }

        public static void Main(string[] args)
        {
            byte podVersion = 5;
            bool compressFiles = false;
            bool verbose = false;
            bool showHelp = false;

            string copyright = string.Empty;
            string author = string.Empty;
            string comment = "Packed with gibbed's Ghostbusters tools";
            string nextName = string.Empty;

            var options = new OptionSet()
            {
                { "c|compress", "overwrite files", v => compressFiles = v != null },
                {
                    "w|version=", "specify POD version (default is 5)", v =>
                                                                        {
                                                                            if (v != null)
                                                                            {
                                                                                podVersion = byte.Parse(v);
                                                                            }
                                                                        }
                },
                { "t|comment=", "set comment text", v => comment = v },
                { "r|author=", "set author text", v => author = v },
                { "y|copyright=", "set copyright text", v => copyright = v },
                { "n|next=", "set next POD name", v => nextName = v },
                { "v|verbose", "show verbose messages", v => verbose = v != null },
                { "h|help", "show this message and exit", v => showHelp = v != null },
            };

            List<string> extras;

            try
            {
                extras = options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("{0}: ", GetExecutableName());
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `{0} --help' for more information.", GetExecutableName());
                return;
            }

            if (extras.Count < 1 || showHelp == true)
            {
                Console.WriteLine("Usage: {0} [OPTIONS]+ output_pod input_directory+", GetExecutableName());
                Console.WriteLine();
                Console.WriteLine("Pack files from input directories into a Big File (FAT/DAT pair).");
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (podVersion < 3 || podVersion > 5)
            {
                Console.WriteLine("Warning: unexpected POD version specified.");
            }

            if (podVersion < 5 && string.IsNullOrEmpty(nextName) == false)
            {
                if (File.Exists(nextName) == false)
                {
                    Console.WriteLine("Cannot specify next POD name for POD files less than version 5.");
                    return;
                }
            }

            var inputPaths = new List<string>();
            string outputPath;

            if (extras.Count == 1)
            {
                inputPaths.Add(extras[0]);
                outputPath = Path.ChangeExtension(extras[0], ".POD");
            }
            else
            {
                outputPath = Path.ChangeExtension(extras[0], ".POD");
                inputPaths.AddRange(extras.Skip(1));
            }

            var pendingEntries = new SortedDictionary<string, PendingEntry>();

            if (verbose == true)
            {
                Console.WriteLine("Finding files...");
            }

            foreach (var relativePath in inputPaths)
            {
                string inputPath = Path.GetFullPath(relativePath);

                if (inputPath.EndsWith(Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture)) == true)
                {
                    inputPath = inputPath.Substring(0, inputPath.Length - 1);
                }

                foreach (string path in Directory.GetFiles(inputPath, "*", SearchOption.AllDirectories))
                {
                    string fullPath = Path.GetFullPath(path);

                    string partPath = fullPath.Substring(inputPath.Length + 1)
                                              .Replace(Path.DirectorySeparatorChar, '\\')
                                              .Replace(Path.AltDirectorySeparatorChar, '\\');

                    var key = partPath.ToLowerInvariant();

                    if (pendingEntries.ContainsKey(key) == true)
                    {
                        Console.WriteLine("Ignoring duplicate of {0}: {1}", partPath, fullPath);

                        if (verbose == true)
                        {
                            Console.WriteLine("  Previously added from: {0}",
                                              pendingEntries[partPath]);
                        }

                        continue;
                    }

                    pendingEntries[key] = new PendingEntry(fullPath, partPath);
                }
            }

            using (var output = File.Create(outputPath))
            {
                var pod = new PodFile()
                {
                    Version = podVersion,
                    Checksum = 0x44424247,
                    Comment = comment,
                    Author = author,
                    Copyright = copyright,
                    Unknown10C = 0x58585858,
                    NextName = nextName,
                };

                output.Seek(pod.HeaderSize, SeekOrigin.Begin);

                if (verbose == true)
                {
                    Console.WriteLine("Writing file data...");
                }

                long current = 0;
                long total = pendingEntries.Count;
                var padding = total.ToString(CultureInfo.InvariantCulture).Length;

                foreach (var pendingEntry in pendingEntries.Select(kv => kv.Value))
                {
                    var entry = new PodFile.Entry();

                    var partPath = pendingEntry.PartPath;
                    var fullPath = pendingEntry.FullPath;

                    current++;

                    if (verbose == true)
                    {
                        Console.WriteLine("[{0}/{1}] {2}",
                                          current.ToString(CultureInfo.InvariantCulture).PadLeft(padding),
                                          total,
                                          partPath);
                    }

                    using (var input = File.OpenRead(fullPath))
                    {
                        entry.Name = partPath;
                        entry.Offset = (uint)output.Position;
                        entry.UncompressedSize = (uint)input.Length;
                        entry.Timestamp = 0x42494720;
                        entry.Checksum = 0x20444542;

                        if (compressFiles == false)
                        {
                            entry.CompressedSize = entry.UncompressedSize;
                            entry.CompressionLevel = 0;
                            output.WriteFromStream(input, input.Length);
                        }
                        else
                        {
                            int compressionLevel = Deflater.BEST_COMPRESSION;
                            uint compressedSize;

                            using (var temp = new MemoryStream())
                            {
                                var zlib = new DeflaterOutputStream(temp, new Deflater(compressionLevel));
                                zlib.WriteFromStream(input, input.Length);
                                zlib.Finish();
                                temp.Flush();
                                temp.Position = 0;

                                compressedSize = (uint)temp.Length;
                                output.WriteFromStream(temp, temp.Length);
                            }

                            entry.CompressedSize = compressedSize;
                            entry.CompressionLevel = 0;
                        }

                        pod.Entries.Add(entry);
                    }
                }

                if (verbose == true)
                {
                    Console.WriteLine("Writing index...");
                }

                long indexOffset;
                uint indexCount, stringSize;
                pod.SerializeIndex(output, out indexOffset, out indexCount, out stringSize);

                if (verbose == true)
                {
                    Console.WriteLine("Writing header...");
                }

                output.Seek(0, SeekOrigin.Begin);
                pod.SerializeHeader(output, indexOffset, indexCount, stringSize);

                if (verbose == true)
                {
                    Console.WriteLine("Done!");
                }
            }
        }
    }
}
