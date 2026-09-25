using System.IO;
using Squishy.Simulation.Save;

namespace Squishy.Runtime.Save
{
    /// <summary>
    /// Saves to a file, writing a temp file first and swapping it in, so a crash mid-write
    /// never leaves a half-written save. The previous save is kept as a .bak.
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        private readonly string _path;

        public FileSaveStore(string path)
        {
            _path = path;
        }

        public string Path { get { return _path; } }

        public bool TryRead(out string text)
        {
            if (File.Exists(_path))
            {
                text = File.ReadAllText(_path);
                return true;
            }
            // A crash between the two moves in Write can leave only the backup.
            string backup = _path + ".bak";
            if (File.Exists(backup))
            {
                text = File.ReadAllText(backup);
                return true;
            }
            text = null;
            return false;
        }

        public void Write(string text)
        {
            string dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string temp = _path + ".tmp";
            string backup = _path + ".bak";
            File.WriteAllText(temp, text);

            if (File.Exists(_path))
            {
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(_path, backup);
            }
            File.Move(temp, _path);
        }

        /// <summary>Keeps an unreadable save next to the real one instead of losing it.</summary>
        public void WriteCorruptCopy(string text)
        {
            File.WriteAllText(_path + ".corrupt", text);
        }
    }
}
