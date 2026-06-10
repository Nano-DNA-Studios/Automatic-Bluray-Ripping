using NanoDNA.ProcessRunner;
using NanoDNA.ProcessRunner.Enums;
using NanoDNA.ProcessRunner.Results;
using System.Text.RegularExpressions;

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

        public MKVFile()
        {
            ID = 0;
            Name = string.Empty;
            Chapters = 0;
            Duration = string.Empty;
            Size = string.Empty;
            IsSelected = false;
            Progress = 0;
        }

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

        public MKVFile[] Files;

        public OpticalDiscBackup(string dirPath)
        {
            Name = Path.GetFileName(dirPath) ?? "";
            DirPath = dirPath;
            Files = [];

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
                return false;

            fullPath = Path.Combine(fullPath, BDMV);

            if (!Directory.Exists(fullPath))
                return false;

            dirs = Directory.GetDirectories(fullPath);

            if (!dirs.Contains(Path.Combine(fullPath, STREAM)))
                return false;

            return true;
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

        public async Task ExtractBackupInfo()
        {
            ProcessRunner process = new ProcessRunner("makemkvcon", workingDirectory: DefaultSettings.DefaultRipDirectory);

            string args = $"-r info file:{Name}/ --minlength={DefaultSettings.MinVideoLength} --noscan";

            List<MKVFile> files = new List<MKVFile>();
            List<string> matches = new();

            process.STDOutputReceived += (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.Data))
                    return;

                string pattern = @"^TINFO:(?<id>\d+),(?<code>\d+),(?<idVal>\d+),""(?<value>[^""]*)""";

                Match match = Regex.Match(args.Data, pattern);

                if (!match.Success)
                    return;

                if (IsValidCode(match))
                    matches.Add(match.Groups["value"].Value);

                if (!IsEndCode(match))
                    return;

                if (matches.Count < 3)
                    return;

                if (matches.Count == 3)
                    matches.Insert(0, "1");

                files.Add(new MKVFile()
                {
                    ID = int.Parse(match.Groups["id"].Value),
                    Chapters = int.Parse(matches[0]),
                    Duration = matches[1],
                    Size = matches[2],
                    Name = matches[3],
                    IsSelected = true
                });

                matches = new List<string>();
            };

            ProcessResult result = (await process.RunAsync(args)).Content;

            if (result.Status == ProcessStatus.Success)
                Files = files.ToArray();
        }

        public async Task CreateMKVs(CancellationToken cancellationToken, MakeMKVManager manager)
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

                bool isUnlocked = false;

                process.STDOutputReceived += (sender, args) =>
                {
                    if (string.IsNullOrEmpty(args.Data))
                        return;

                    if (!isUnlocked)
                    {
                        string unlockPattern = @"^PRGC:(?<currentProgress>\d+),(?<globalProgress>\d+),""(?<value>[^""]+)""$";

                        Match unlockMatch = Regex.Match(args.Data, unlockPattern);

                        isUnlocked = unlockMatch.Success && unlockMatch.Groups["value"].Value == "Saving to MKV file";

                        return;
                    }

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

                await process.RunAsync(args);

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

        public async Task ScanForBackups()
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

                DiscBackups.Add(backup);

                await backup.ExtractBackupInfo();

                RaiseProgressUpdate();
            }
        }

        public void RaiseProgressUpdate()
        {
            OnProgressUpdated?.Invoke();
        }

        public async Task CreateMKVs()
        {
            foreach (OpticalDiscBackup backup in DiscBackups)
            {
                await backup.CreateMKVs(TokenSrc.Token, this);
            }
        }
    }
}
