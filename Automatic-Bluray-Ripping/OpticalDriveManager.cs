using NanoDNA.AutomationResults;
using NanoDNA.ProcessRunner;
using System.Text.RegularExpressions;

namespace Automatic_Bluray_Ripping
{
    public class OpticalDrive
    {
        public int ID { get; set; }

        public string DriveName { get; set; }

        public string DiscName { get; set; }

        public string DrivePath { get; set; }

        public double Progress { get; set; }

        public double GlobalProgress { get; set; }

        public bool IsBusy { get; set; }

        public OpticalDrive(int id, string driveName, string discName, string drivePath)
        {
            ID = id;
            DriveName = driveName;
            DiscName = discName;
            DrivePath = drivePath;

            Progress = 0;
            GlobalProgress = 0;
        }
    }

    public class OpticalDriveManager
    {
        public OpticalDrive[] OpticalDrives { get; set; }

        public bool HasScanned { get; set; }
        public bool IsScanning { get; set; }

        public bool IsLocked { get; set; }

        public CancellationTokenSource TokenSrc { get; }

        public event Action? OnProgressUpdated;

        private DefaultSettings _settings { get; }

        public OpticalDriveManager(DefaultSettings settings)
        {
            _settings = settings;

            OpticalDrives = new OpticalDrive[0];
            TokenSrc = new CancellationTokenSource();

            HasScanned = false;
            IsScanning = false;
            IsLocked = false;
        }

        public async Task ReadOpticalDrives()
        {
            List<OpticalDrive> drives = new List<OpticalDrive>();

            HasScanned = false;
            IsScanning = true;

            Console.WriteLine("Scanning Optical drives");

            ProcessRunner process = new ProcessRunner("makemkvcon");
            string args = "-r --cache=1 info disc:9999";

            process.STDOutputReceived += (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.Data))
                    return;

                Console.WriteLine(args.Data);

                string pattern = @"^DRV:(?<index>\d+),(?<visible>\d+),(?<id>\d+),(?<type>\d+),""(?<name>[^""]*)"",""(?<discname>[^""]*)"",""(?<drivepath>[^""]*)""";

                Match match = Regex.Match(args.Data, pattern);

                if (!match.Success)
                    return;

                int driveID = int.Parse(match.Groups["index"].Value);
                string driveName = match.Groups["name"].Value;
                string blurayName = match.Groups["discname"].Value;
                string drivePath = match.Groups["drivepath"].Value;

                if (string.IsNullOrEmpty(driveName))
                    return;

                drives.Add(new OpticalDrive(driveID, driveName, blurayName, drivePath));
            };

            process.STDErrorReceived += (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.Data))
                    return;

                Console.WriteLine(args.Data);
            };

            //Convert to Async with Cancellation token
            Result<int> result = (await process.RunAsync(args));

            Console.WriteLine("Finished Scanning Optical Drives");

            if (result.IsSuccess)
                this.OpticalDrives = drives.ToArray();

            IsScanning = false;
            HasScanned = true;

            RaiseProgressUpdate();
        }

        public async Task RipOpticalDiscs()
        {
            List<Task> ripTasks = new List<Task>();

            IsLocked = true;

            foreach (OpticalDrive drive in OpticalDrives)
            {
                Task ripTask = RipOpticalDisc(TokenSrc.Token, drive);
                ripTasks.Add(ripTask);
            }

            await Task.WhenAll(ripTasks);

            IsLocked = false;

            RaiseProgressUpdate();
        }

        public void RaiseProgressUpdate()
        {
            OnProgressUpdated?.Invoke();
        }

        public bool IsBusy(string name)
        {
            return OpticalDrives.Any(drive => drive.DiscName == name && drive.IsBusy);
        }

        public async Task RipOpticalDisc(CancellationToken cancellationToken, OpticalDrive drive)
        {
            drive.IsBusy = true;

            ProcessRunner process = new ProcessRunner("makemkvcon");

            string args = $"backup --decrypt --cache=1024 --noscan -r --progress=-same --minlength={_settings.MinVideoLength} disc:{drive.ID} \"{Path.Combine(_settings.DefaultRipDirectory, drive.DiscName)}\"";

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

                drive.Progress = currentProgress;
                drive.GlobalProgress = globalProgress;
                RaiseProgressUpdate();
            };

            //Add the cancellation token here
            Result<int> result = (await process.RunAsync(args));

            if (result.IsSuccess)
            {
                drive.Progress = 1;
                drive.GlobalProgress = 1;
                await EjectDisc(drive);
                RaiseProgressUpdate();
            }

            drive.IsBusy = false;
        }

        public async Task EjectDisc(OpticalDrive opticalDrive)
        {
            ProcessRunner process = new ProcessRunner("eject");

            Result<int> result = await process.RunAsync(opticalDrive.DrivePath);

            if (result.IsSuccess)
                Console.WriteLine($"Successfully Ejected Drive {opticalDrive.DriveName} ({opticalDrive.DrivePath})");
            else
                Console.WriteLine($"Failed to Eject Drive {opticalDrive.DriveName} ({opticalDrive.DrivePath})");
        }
    }
}
