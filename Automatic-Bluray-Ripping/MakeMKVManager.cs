using NanoDNA.ProcessRunner;
using NanoDNA.ProcessRunner.Enums;
using NanoDNA.ProcessRunner.Results;
using System.Security.Cryptography;
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
        public string Name { get; set; }

        public string DirPath { get; set; }

        public double GlobalProgress { get; set; }

        public bool IsScanning { get; set; }

        public bool IsConverting { get; set; }

        public MKVFile[] Files { get; set; }

        public bool RemoveOnCompletion { get; set; }

        public OpticalDiscBackup(string dirPath)
        {
            Name = Path.GetFileName(dirPath) ?? "";
            DirPath = dirPath;
            Files = [];

            GlobalProgress = 0;
            IsScanning = false;
            IsConverting = false;
            RemoveOnCompletion = true;
        }
    }

    public class MakeMKVManager
    {
        private const string BDMV = "BDMV";
        private const string STREAM = "STREAM";

        private const int CHAPTER_CODE = 8;
        private const int DURATION_CODE = 9;
        private const int SIZE_CODE = 10;
        private const int NAME_CODE = 27;
        private const int ENDING_CODE = 33;

        private int[] ValidCodes { get; }

        private Regex UnlockRegex { get; }

        private Regex ProgressRegex { get; }

        private Regex TitleInfoRegex { get; }

        public List<OpticalDiscBackup> DiscBackups { get; private set; }

        public CancellationTokenSource TokenSrc { get; }

        public bool IsScanning { get; private set; }

        public bool IsConverting { get; private set; }

        public event Action? OnProgressUpdated;

        private OpticalDriveManager _opticalDriveManager;

        public MakeMKVManager(OpticalDriveManager opticalDriveManager)
        {
            DiscBackups = new List<OpticalDiscBackup>();
            TokenSrc = new CancellationTokenSource();

            UnlockRegex = new(@"^PRGC:(?<currentProgress>\d+),(?<globalProgress>\d+),""(?<value>[^""]+)""$", RegexOptions.Compiled);
            ProgressRegex = new(@"^PRGV:(?<currentProgress>\d+),(?<globalProgress>\d+),(?<total>\d+)", RegexOptions.Compiled);
            TitleInfoRegex = new(@"^TINFO:(?<id>\d+),(?<code>\d+),(?<idVal>\d+),""(?<value>[^""]*)""", RegexOptions.Compiled);

            ValidCodes = [CHAPTER_CODE, DURATION_CODE, SIZE_CODE, NAME_CODE];

            IsScanning = true;

            _opticalDriveManager = opticalDriveManager;
        }

        private bool InProgress(OpticalDiscBackup backup)
        {
            return DiscBackups.Any((d) => d.Name == backup.Name);
        }

        public async Task ScanForBackups()
        {
            IsScanning = true;

            DiscBackups = DiscBackups.Where((d) => d.IsConverting).ToList();

            string[] dirs = Directory.EnumerateDirectories(DefaultSettings.DefaultRipDirectory).ToArray();

            foreach (string dir in dirs)
            {
                OpticalDiscBackup backup = new OpticalDiscBackup(dir);

                if (string.IsNullOrEmpty(backup.Name))
                    continue;

                if (InProgress(backup))
                    continue;

                if (!IsBlurayBackup(backup))
                    continue;

                if (_opticalDriveManager.IsBusy(backup.Name))
                    continue;

                backup.IsScanning = true;

                DiscBackups.Add(backup);
            }

            foreach (OpticalDiscBackup backup in DiscBackups)
                await ExtractBackupInfo(backup, TokenSrc.Token);

            IsScanning = false;

            RaiseProgressUpdate();
        }

        public bool IsBlurayBackup(OpticalDiscBackup backup)
        {
            string fullPath = backup.DirPath;
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

            return intCode == ENDING_CODE;
        }

        public async Task ExtractBackupInfo(OpticalDiscBackup backup, CancellationToken token)
        {
            backup.IsScanning = true;

            List<MKVFile> files = new List<MKVFile>();
            List<string> matches = new List<string>();

            ProcessRunner process = new ProcessRunner("makemkvcon", workingDirectory: DefaultSettings.DefaultRipDirectory);

            string args = $"-r info file:{backup.Name}/ --minlength={DefaultSettings.MinVideoLength} --noscan";

            process.STDOutputReceived += (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.Data))
                    return;

                Match match = TitleInfoRegex.Match(args.Data);

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
                backup.Files = files.ToArray();

            backup.IsScanning = false;

            RaiseProgressUpdate();
        }

        public void RaiseProgressUpdate()
        {
            OnProgressUpdated?.Invoke();
        }

        public async Task MakeAllMKVs()
        {
            IsConverting = true;

            foreach (OpticalDiscBackup backup in DiscBackups)
                backup.IsConverting = true;

            foreach (OpticalDiscBackup backup in DiscBackups)
            {
                await MakeMKVs(backup);
                backup.IsConverting = false;
            }

            IsConverting = false;
        }

        public async Task MakeMKVs(OpticalDiscBackup backup)
        {
            backup.IsConverting = true;

            backup.Files = backup.Files.Where((f) => f.IsSelected).ToArray();

            double offset = 0;
            double weight = 1 / (double)backup.Files.Count();

            foreach (MKVFile file in backup.Files)
            {
                await MakeMKV(file, backup, offset, weight);

                file.Progress = 1;
                offset += weight;
                RaiseProgressUpdate();
            }

            backup.IsConverting = false;

            if (backup.RemoveOnCompletion)
                RemoveOpticalDiscBackup(backup);
        }

        private async Task MakeMKV(MKVFile file, OpticalDiscBackup backup, double offset, double weight)
        {
            ProcessRunner process = new ProcessRunner("makemkvcon", workingDirectory: DefaultSettings.DefaultRipDirectory);

            string output = Path.Combine(DefaultSettings.DefaultMKVDirectory, backup.Name);
            string args = $"mkv file:{backup.Name} {file.ID} \"{output}\" --cache=1024 --noscan --minlength={DefaultSettings.MinVideoLength} -r --progress=-same";

            bool isUnlocked = false;

            if (!Directory.Exists(output))
                Directory.CreateDirectory(output);

            process.STDOutputReceived += (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.Data))
                    return;

                if (!isUnlocked)
                {
                    Match unlockMatch = UnlockRegex.Match(args.Data);

                    isUnlocked = unlockMatch.Success && unlockMatch.Groups["value"].Value == "Saving to MKV file";

                    return;
                }

                Match match = ProgressRegex.Match(args.Data);

                if (!match.Success)
                    return;

                double total = double.Parse(match.Groups["total"].Value);
                double currentProgress = double.Parse(match.Groups["currentProgress"].Value) / total;

                backup.GlobalProgress = offset + currentProgress * weight;
                file.Progress = currentProgress;
                RaiseProgressUpdate();
            };

            //Add the Cancellation token later
            await process.RunAsync(args);
        }

        private void RemoveOpticalDiscBackup(OpticalDiscBackup backup)
        {
            Directory.Delete(backup.DirPath, true);

            DiscBackups.Remove(backup);
        }
    }
}
