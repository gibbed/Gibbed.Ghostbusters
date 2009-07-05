using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Gibbed.Ghostbusters.FileFormats;
using Ionic.Zlib;

namespace Gibbed.Ghostbusters.Unpack
{
	public partial class Extractor : Form
	{
		public Extractor()
		{
			this.InitializeComponent();
		}

		delegate void SetProgressDelegate(long percent);
		private void SetProgress(long percent)
		{
			if (this.progressBar.InvokeRequired)
			{
				SetProgressDelegate callback = new SetProgressDelegate(SetProgress);
				this.Invoke(callback, new object[] { percent });
				return;
			}
			
			this.progressBar.Value = (int)percent;
		}

		delegate void LogDelegate(string message);
		private void Log(string message)
		{
			if (this.logText.InvokeRequired)
			{
				LogDelegate callback = new LogDelegate(Log);
				this.Invoke(callback, new object[] { message });
				return;
			}

			if (this.logText.Text.Length == 0)
			{
				this.logText.AppendText(message);
			}
			else
			{
				this.logText.AppendText(Environment.NewLine + message);
			}
		}

		delegate void EnableButtonsDelegate(bool extract);
		private void EnableButtons(bool extract)
		{
			if (this.extractButton.InvokeRequired || this.cancelButton.InvokeRequired)
			{
				EnableButtonsDelegate callback = new EnableButtonsDelegate(EnableButtons);
				this.Invoke(callback, new object[] { extract });
				return;
			}

			this.extractButton.Enabled = extract ? true : false;
			this.cancelButton.Enabled = extract ? false : true;
		}

		private void OnOpen(object sender, EventArgs e)
		{
            this.logText.Clear();

			if (this.openFileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			string podPath = this.openFileDialog.FileName;
            string savePath = null;
            
            /*
			string savePath = Path.GetDirectoryName(podPath);
			Directory.CreateDirectory(savePath);
			this.savePathDialog.SelectedPath = savePath;
            */
			
            if (this.savePathDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}
			
            savePath = this.savePathDialog.SelectedPath;

            PodFile pod = new PodFile();
			Stream stream = File.OpenRead(podPath);
			pod.Deserialize(stream);
			stream.Close();

			this.progressBar.Minimum = 0;
			this.progressBar.Maximum = pod.Entries.Count;
			this.progressBar.Value = 0;

			ExtractThreadInfo info = new ExtractThreadInfo();
			info.SavePath = savePath;
			info.PodPath = podPath;
			info.PodFile = pod;

			this.ExtractThread = new Thread(new ParameterizedThreadStart(ExtractFiles));
			this.ExtractThread.Start(info);
			this.EnableButtons(false);
		}

		private Thread ExtractThread;
		private class ExtractThreadInfo
		{
			public string SavePath;
			public string PodPath;
			public PodFile PodFile;
		}

		public void ExtractFiles(object oinfo)
		{
			long succeeded, failed, current;
			ExtractThreadInfo info = (ExtractThreadInfo)oinfo;

			Stream input = File.OpenRead(info.PodPath);

			succeeded = failed = current = 0;

			this.Log(String.Format("{0} files in pod.", info.PodFile.Entries.Count));

			foreach (PodEntry entry in info.PodFile.Entries)
			{
				this.SetProgress(++current);

				input.Seek(entry.Offset, SeekOrigin.Begin);

				string outputName = entry.Name;
				this.Log(outputName);

                string outputPath = Path.Combine(info.SavePath, Path.GetDirectoryName(entry.Name));
                Directory.CreateDirectory(outputPath);

				Stream output = File.OpenWrite(Path.Combine(info.SavePath, outputName));

                if (entry.CompressionLevel > 0)
                {
                    ZlibStream zlib = new ZlibStream(input, CompressionMode.Decompress, true);
                    uint left = entry.UncompressedSize;
                    byte[] block = new byte[4096];
                    while (left > 0)
                    {
                        int read = zlib.Read(block, 0, (int)Math.Min(block.Length, left));
                        
                        if (read == 0)
                        {
                            break;
                        }
                        else if (read < 0)
                        {
                            throw new InvalidOperationException("decompression error");
                        }

                        output.Write(block, 0, read);
                        left -= (uint)read;
                    }
                    zlib.Close();
                }
                else
                {
                    long left = entry.UncompressedSize;
                    byte[] data = new byte[4096];
                    while (left > 0)
                    {
                        int block = (int)(Math.Min(left, 4096));
                        input.Read(data, 0, block);
                        output.Write(data, 0, block);
                        left -= block;
                    }
                }

				output.Close();
				succeeded++;
			}

			input.Close();

			this.Log(String.Format("Done, {0} succeeded, {1} failed, {2} total.", succeeded, failed, info.PodFile.Entries.Count));
			this.EnableButtons(true);
		}

		private void OnCancel(object sender, EventArgs e)
		{
			if (this.ExtractThread != null)
			{
				this.ExtractThread.Abort();
			}

			this.Close();
		}

		private void OnLoad(object sender, EventArgs e)
		{
			string path = null;

            path = (string)Microsoft.Win32.Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Atari\\Ghostbusters", "InstallPath", null);

			if (path == null)
			{
                path = (string)Microsoft.Win32.Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App 9870", "InstallLocation", null);
			}

			this.openFileDialog.InitialDirectory = path;
		}
	}
}
