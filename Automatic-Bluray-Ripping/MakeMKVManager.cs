using System.IO;

namespace Automatic_Bluray_Ripping
{
    public class OpticalDiscBackup
    {
        private const string BDMV = "BDMV";
        private const string STREAM = "STREAM";

        public int ID { get; set; }

        public string Name { get; set; }

        public string DirPath { get; set; }


        public OpticalDiscBackup (int id, string name, string path)
        {

        }

        private bool IsBlurayBackup(string basePath)
        {
            string fullPath = basePath;
            string[] dirs;

            if (!Directory.Exists(fullPath))
                return false;

            dirs = Directory.GetDirectories(fullPath);

            if (!dirs.Contains(Path.Combine(fullPath, BDMV)))
            {
                //Console.WriteLine("No BDMV Folder");
                return false;
            }

            fullPath = Path.Combine(fullPath, BDMV);

            if (!Directory.Exists(fullPath))
                return false;

            dirs = Directory.GetDirectories(fullPath);

            if (!dirs.Contains(Path.Combine(fullPath, STREAM)))
            {
                //Console.WriteLine("No STREAM Folder");
                return false;
            }

            return true;
        }

        private string GetBlurayStreamDir(string basePath)
        {
            return Path.Combine(basePath, BDMV, STREAM);
        }
    }




    public class MakeMKVManager
    {



        

       

    }
}
