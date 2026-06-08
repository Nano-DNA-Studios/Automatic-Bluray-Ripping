using NanoDNA.ProcessRunner;
using NanoDNA.ProcessRunner.Enums;
using NanoDNA.ProcessRunner.Results;
using System.Text.RegularExpressions;

namespace Automatic_Bluray_Ripping
{
    public class OpticalDrive
    {
        public int ID { get; set; }

        public string DriveName { get; set; }

        public string DiscName { get; set; }

        public double Progress { get; set; }

        public double GlobalProgress { get; set; }

        public bool IsBusy { get; set; }

        public OpticalDrive(int id, string driveName, string discName)
        {
            ID = id;
            DriveName = driveName;
            DiscName = discName;

            Progress = 0;
            GlobalProgress = 0;
        }

        public async Task RipOpticalDisc(CancellationToken cancellationToken, OpticalDriveManager manager)
        {
            IsBusy = true;

            ProcessRunner process = new ProcessRunner("makemkvcon");

            string args = $"backup --decrypt --cache=1024 --noscan -r --progress=-same --minlength={DefaultSettings.MinVideoLength} disc:{ID} \"{Path.Combine(DefaultSettings.DefaultRipDirectory, DiscName)}\"";

            Console.WriteLine($"Running : {args}");

            process.STDOutputReceived += (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.Data))
                    return;

                Console.WriteLine(args.Data);

                string pattern = @"^PRGV:(?<currentProgress>\d+),(?<globalProgress>\d+),(?<total>\d+)";

                Match match = Regex.Match(args.Data, pattern);

                if (!match.Success)
                    return;

                double total = double.Parse(match.Groups["total"].Value);
                double currentProgress = double.Parse(match.Groups["currentProgress"].Value) / total;
                double globalProgress = double.Parse(match.Groups["globalProgress"].Value) / total;

                Progress = currentProgress;
                GlobalProgress = globalProgress;
                manager.RaiseProgressUpdate();
            };

            ProcessResult result = process.Run(args).Content;

            if (result.Status == ProcessStatus.Success)
            {
                Progress = 1;
                GlobalProgress = 1;
                manager.RaiseProgressUpdate();
            }

            IsBusy = false;
        }
    }

    public class OpticalDriveManager
    {
        public OpticalDrive[] OpticalDrives { get; set; } = [];

        public bool HasScanned = false;
        public bool IsScanning = false;

        public bool IsLocked = false;

        public CancellationTokenSource TokenSrc = new ();

        public event Action? OnProgressUpdated;

        public async Task ReadOpticalDrives()
        {
            List<OpticalDrive> drives = new List<OpticalDrive>();

            IsScanning = true;

            ProcessRunner process = new ProcessRunner("makemkvcon");
            string args = "-r --cache=1 info disc:9999";

            process.STDOutputReceived += (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.Data))
                    return;

                string pattern = @"^DRV:(?<index>\d+),(?<visible>\d+),(?<id>\d+),(?<type>\d+),""(?<name>[^""]*)"",""(?<discname>[^""]*)"",""(?<letter>[A-Z]:)?""$";

                Match match = Regex.Match(args.Data, pattern);

                if (!match.Success)
                    return;

                int driveID = int.Parse(match.Groups["index"].Value);
                string driveName = match.Groups["name"].Value;
                string blurayName = match.Groups["discname"].Value;

                if (string.IsNullOrEmpty(driveName))
                    return;

                drives.Add(new OpticalDrive(driveID, driveName, blurayName));
            };

            ProcessResult result = process.Run(args).Content;

            if (result.Status == ProcessStatus.Success)
                this.OpticalDrives = drives.ToArray();

            IsScanning = false;
            HasScanned = true;
        }

        public async Task RipOpticalDiscs()
        {
            List<Task> ripTasks = new List<Task>();

            IsLocked = true;

            foreach (OpticalDrive drive in OpticalDrives)
            {
                Task ripTask = drive.RipOpticalDisc(TokenSrc.Token, this);
                ripTasks.Add(ripTask);
            }

            await Task.WhenAll(ripTasks);

            IsLocked = false;
        }

        public void RaiseProgressUpdate()
        {
            OnProgressUpdated?.Invoke();
        }

        public bool IsBusy(string name)
        {
           return OpticalDrives.Any(drive => drive.DiscName == name && drive.IsBusy);
        }

        //public async Task RipOpticalDisc(OpticalDrive drive, CancellationToken cancellationToken)
        //{
        //    ProcessRunner process = new ProcessRunner("makemkvcon");

        //    string args = $"backup --decrypt --cache=1024 --noscan -r --progress=-same --min-length=1 disc:{drive.ID} \"{Path.Combine(DefaultSettings.DefaultRipDirectory, drive.DiscName)}\"";

        //    Console.WriteLine($"Running : {args}");

        //    process.STDOutputReceived += (sender, args) =>
        //    {
        //        if (string.IsNullOrEmpty(args.Data))
        //            return;

        //        Console.WriteLine(args.Data);

        //        string pattern = @"^PRGV:(?<currentProgress>\d+),(?<globalProgress>\d+),(?<total>\d+)";

        //        Match match = Regex.Match(args.Data, pattern);

        //        if (!match.Success)
        //            return;

        //        double total = double.Parse(match.Groups["total"].Value);
        //        double currentProgress = double.Parse(match.Groups["currentProgress"].Value) / total;
        //        double globalProgress = double.Parse(match.Groups["globalProgress"].Value) / total;

        //        drive.Progress = currentProgress;
        //        drive.GlobalProgress = globalProgress;

        //        OnProgressUpdated?.Invoke();
        //    };

        //    process.STDErrorReceived += (sender, args) =>
        //    {
        //        Console.WriteLine(args.Data);
        //    };

        //    ProcessResult result = process.Run(args).Content;

        //    if (result.Status == ProcessStatus.Success)
        //    {
        //        drive.Progress = 1;
        //        drive.GlobalProgress = 1;
        //        OnProgressUpdated?.Invoke();
        //    }
        //}
    }
}
