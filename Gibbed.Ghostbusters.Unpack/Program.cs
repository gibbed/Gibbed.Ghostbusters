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
using System.Text.RegularExpressions;
using Gibbed.Ghostbusters.FileFormats;
using Gibbed.IO;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using NDesk.Options;

namespace Gibbed.Ghostbusters.Unpack
{
    internal class Program
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        public static void Main(string[] args)
        {
            bool showHelp = false;
            string filterPattern = null;
            bool overwriteFiles = false;
            bool verbose = false;

            var options = new OptionSet()
            {
                { "o|overwrite", "overwrite existing files", v => overwriteFiles = v != null },
                { "f|filter=", "only extract files using pattern", v => filterPattern = v },
                { "v|verbose", "be verbose", v => verbose = v != null },
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

            if (extras.Count < 1 || extras.Count > 2 || showHelp == true)
            {
                Console.WriteLine("Usage: {0} [OPTIONS]+ input_fat [output_dir]", GetExecutableName());
                Console.WriteLine();
                Console.WriteLine("Unpack files from a Big File (FAT/DAT pair).");
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            string inputPath = extras[0];
            string outputPath = extras.Count > 1 ? extras[1] : Path.ChangeExtension(inputPath, null) + "_unpack";

            Regex filter = null;
            if (string.IsNullOrEmpty(filterPattern) == false)
            {
                filter = new Regex(filterPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }

            using (var input = File.OpenRead(inputPath))
            {
                var pod = new PodFile();
                pod.Deserialize(input);

                if (verbose == true)
                {
                    Console.WriteLine("Version: {0}", pod.Version);
                }

                if (verbose == true && string.IsNullOrEmpty(pod.Comment) == false)
                {
                    Console.WriteLine("Comment: {0}", pod.Comment);
                }

                if (verbose == true && string.IsNullOrEmpty(pod.Author) == false)
                {
                    Console.WriteLine("Author: {0}", pod.Author);
                }

                if (verbose == true && string.IsNullOrEmpty(pod.Copyright) == false)
                {
                    Console.WriteLine("Copyright: {0}", pod.Copyright);
                }

                if (verbose == true && string.IsNullOrEmpty(pod.NextName) == false)
                {
                    Console.WriteLine("Next POD: {0}", pod.NextName);
                }

                long current = 0;
                long total = pod.Entries.Count;
                var padding = total.ToString(CultureInfo.InvariantCulture).Length;

                foreach (var entry in pod.Entries)
                {
                    current++;

                    var entryName = entry.Name;
                    entryName = entryName.Replace('/', Path.DirectorySeparatorChar);
                    entryName = entryName.Replace('\\', Path.DirectorySeparatorChar);

                    if (filter != null &&
                        filter.IsMatch(entryName) == false)
                    {
                        continue;
                    }

                    var entryPath = Path.Combine(outputPath, entryName);
                    if (overwriteFiles == false &&
                        File.Exists(entryPath) == true)
                    {
                        continue;
                    }

                    if (verbose == true)
                    {
                        Console.WriteLine("[{0}/{1}] {2}",
                                          current.ToString(CultureInfo.InvariantCulture).PadLeft(padding),
                                          total,
                                          entryName);
                    }

                    input.Seek(entry.Offset, SeekOrigin.Begin);

                    var entryParent = Path.GetDirectoryName(entryPath);
                    if (string.IsNullOrEmpty(entryParent) == false)
                    {
                        Directory.CreateDirectory(entryParent);
                    }

                    using (var output = File.Create(entryPath))
                    {
                        if (entry.CompressionLevel != 0)
                        {
                            var zlib = new InflaterInputStream(input, new Inflater(true));
                            output.WriteFromStream(zlib, entry.UncompressedSize);
                        }
                        else
                        {
                            output.WriteFromStream(input, entry.UncompressedSize);
                        }
                    }
                }
            }
        }
    }
}
