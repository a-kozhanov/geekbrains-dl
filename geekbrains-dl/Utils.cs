using System;
using System.IO;

namespace GeekBrainsDownloader
{
    public class Utils
    {
        public static void CreateDir(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    DirectoryInfo di = Directory.CreateDirectory(path);
                }
            }
            catch (IOException ioex)
            {
                Console.WriteLine(ioex.Message);
            }
        }
    }
}