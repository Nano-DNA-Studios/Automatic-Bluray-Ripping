using NanoDNA.ProcessRunner;
using NanoDNA.ProcessRunner.Results;
using System.Text.RegularExpressions;
using NanoDNA.ProcessRunner.Enums;

namespace Automatic_Bluray_Ripping
{
    public class MKVFile
    {
        public int ID { get; set; }

        public string Name { get; set; }

        public int Chapters { get; set; }

        public string Duration { get; set; }

        public string Size { get; set; }

        public bool IsSelected { get; set; }

        public double Progress { get; set; }

        public void ToggleSelection()
        {
            IsSelected = !IsSelected;
        }
    }

    public class OpticalDiscBackup
    {
        private const string BDMV = "BDMV";
        private const string STREAM = "STREAM";

        public string Name { get; set; }

        public string DirPath { get; set; }

        public double GlobalProgress { get; set; }

        public bool IsBusy { get; set; }

        private int[] ValidCodes = [8, 9, 10, 27];
        private int EndingCode = 33;

        public MKVFile[] Files = [];

        public OpticalDiscBackup(string dirPath)
        {
            Name = Path.GetFileName(dirPath) ?? "";
            DirPath = dirPath;

            GlobalProgress = 0;
        }

        public bool IsBlurayBackup()
        {
            string fullPath = DirPath;
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

        public string GetBlurayStreamDir()
        {
            return Path.Combine(DirPath, BDMV, STREAM);
        }

        private bool IsValidCode(Match match)
        {
            int intCode = int.Parse(match.Groups["code"].Value);

            return ValidCodes.Contains(intCode);
        }

        private bool IsEndCode(Match match)
        {
            int intCode = int.Parse(match.Groups["code"].Value);

            return intCode == EndingCode;
        }

        public void ExtractBackupInfo()
        {
            ProcessRunner process = new ProcessRunner("makemkvcon", workingDirectory: DefaultSettings.DefaultRipDirectory);

            string args = $"-r info file:{Name}/ --minlength={DefaultSettings.MinVideoLength}";

            List<MKVFile> files = new List<MKVFile>();
            List<Match> matches = new();

            process.STDOutputReceived += (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.Data))
                    return;

                string pattern = @"^TINFO:(?<id>\d+),(?<code>\d+),(?<idVal>\d+),""(?<value>[^""]*)""";

                Match match = Regex.Match(args.Data, pattern);

                if (!match.Success)
                    return;

                if (IsValidCode(match))
                    matches.Add(match);

                if (IsEndCode(match))
                {
                    if (matches.Count < 3)
                        return;

                    MKVFile file;

                    if (matches.Count == 4)
                    {
                        file = new()
                        {
                            ID = int.Parse(matches[0].Groups["id"].Value),
                            Chapters = int.Parse(matches[0].Groups["value"].Value),
                            Duration = matches[1].Groups["value"].Value,
                            Size = matches[2].Groups["value"].Value,
                            Name = matches[3].Groups["value"].Value,
                            IsSelected = true
                        };
                    }
                    else
                    {
                        file = new()
                        {
                            ID = int.Parse(matches[0].Groups["id"].Value),
                            Chapters = 1,
                            Duration = matches[0].Groups["value"].Value,
                            Size = matches[1].Groups["value"].Value,
                            Name = matches[2].Groups["value"].Value,
                            IsSelected = true
                        };
                    }

                    files.Add(file);

                    matches = new();
                }
            };

            ProcessResult result = process.Run(args).Content;

            if (result.Status == ProcessStatus.Success)
                Files = files.ToArray();
        }

        public void CreateMKVs(CancellationToken cancellationToken, MakeMKVManager manager)
        {
            double progress = 0;
            double weight = 1 / (double)Files.Count();

            foreach (MKVFile file in Files)
            {
                if (!file.IsSelected)
                    continue;

                ProcessRunner process = new ProcessRunner("makemkvcon", workingDirectory: DefaultSettings.DefaultRipDirectory);

                string output = Path.Combine(DefaultSettings.DefaultMKVDirectory, Name);
                string args = $"mkv file:{Name} {file.ID} \"{output}\" --cache=1024 --noscan --minlength={DefaultSettings.MinVideoLength} -r --progress=-same";

                if (!Directory.Exists(output))
                    Directory.CreateDirectory(output);

                process.STDOutputReceived += (sender, args) =>
                {
                    if (string.IsNullOrEmpty(args.Data))
                        return;

                    string pattern = @"^PRGV:(?<currentProgress>\d+),(?<globalProgress>\d+),(?<total>\d+)";

                    Match match = Regex.Match(args.Data, pattern);

                    if (!match.Success)
                        return;

                    double total = double.Parse(match.Groups["total"].Value);
                    double currentProgress = double.Parse(match.Groups["currentProgress"].Value) / total;
                    double globalProgress = double.Parse(match.Groups["globalProgress"].Value) / total;

                    GlobalProgress = progress + currentProgress * weight;
                    file.Progress = currentProgress;
                    manager.RaiseProgressUpdate();
                };

                ProcessResult result = process.Run(args).Content;

                file.Progress = 1;
                progress += weight;
                manager.RaiseProgressUpdate();
            }
        }
    }

    public class MakeMKVManager
    {
        public List<OpticalDiscBackup> DiscBackups = new();

        public event Action? OnProgressUpdated;

        public CancellationTokenSource TokenSrc = new();

        public void ScanForBackups()
        {
            OpticalDriveManager? driveManager = AppServices.Get<OpticalDriveManager>();

            string[] dirs = Directory.EnumerateDirectories(DefaultSettings.DefaultRipDirectory).ToArray();

            foreach (string dir in dirs)
            {
                OpticalDiscBackup backup = new OpticalDiscBackup(dir);

                if (string.IsNullOrEmpty(backup.Name))
                    continue;

                if (!backup.IsBlurayBackup())
                    continue;

                if (driveManager != null && driveManager.IsBusy(backup.Name))
                    continue;

                _ = Task.Run(() =>
                {
                    backup.ExtractBackupInfo();
                    RaiseProgressUpdate();
                });

                DiscBackups.Add(backup);
                RaiseProgressUpdate();
            }
        }

        public void RaiseProgressUpdate()
        {
            OnProgressUpdated?.Invoke();
        }

        public void CreateMKVs()
        {
            foreach (OpticalDiscBackup backup in DiscBackups)
            {
                backup.CreateMKVs(TokenSrc.Token, this);
            }
        }
    }
}
