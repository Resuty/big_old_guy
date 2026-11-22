using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class WindowsLauncher
{
    [STAThread]
    private static int Main()
    {
        using (var mutex = new Mutex(false, @"Local\BigOldGuyPortable"))
        {
            if (!mutex.WaitOne(0)) return 0;
            try
            {
                string exe = Assembly.GetExecutingAssembly().Location;
                string directory = Path.Combine(Path.GetDirectoryName(exe), Path.GetFileNameWithoutExtension(exe));
                if (!File.Exists(Path.Combine(directory, ".big-old-guy-complete")))
                {
                    if (Directory.Exists(directory))
                        throw new IOException("同名目录已存在。请将下载的 EXE 移到一个空目录后重试。");
                    string staging = directory + ".unpacking-" + Guid.NewGuid().ToString("N");
                    Directory.CreateDirectory(staging);
                    using (Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("BigOldGuy.Payload"))
                    using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
                    {
                        string prefix = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
                        foreach (var entry in archive.Entries)
                        {
                            string path = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidDataException("游戏包包含无效路径。");
                            if (entry.Name.Length == 0) Directory.CreateDirectory(path);
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(path));
                                using (Stream input = entry.Open())
                                using (Stream output = File.Open(path, FileMode.CreateNew))
                                    input.CopyTo(output);
                            }
                        }
                    }
                    File.WriteAllText(Path.Combine(staging, ".big-old-guy-complete"), "big old guy");
                    Directory.Move(staging, directory);
                }

                using (var game = Process.Start(new ProcessStartInfo(Path.Combine(directory, "BigOldGuy.exe"))
                {
                    WorkingDirectory = directory,
                    UseShellExecute = false
                }))
                {
                    game.WaitForExit();
                    return game.ExitCode;
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "big old guy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            finally { mutex.ReleaseMutex(); }
        }
    }
}
