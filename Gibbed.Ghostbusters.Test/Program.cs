using System;
using System.IO;
using Gibbed.Ghostbusters.FileFormats;

namespace Gibbed.Ghostbusters.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (string path in Directory.GetFiles("T:\\Games\\Singleplayer\\Ghostbusters", "*.POD"))
            {
                Stream input = File.OpenRead(path);
                PodFile pod = new PodFile();
                pod.Deserialize(input);
                input.Close();
            }
        }
    }
}
