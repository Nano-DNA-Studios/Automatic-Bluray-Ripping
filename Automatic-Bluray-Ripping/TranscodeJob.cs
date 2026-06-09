namespace Automatic_Bluray_Ripping
{
    public enum TranscodeStatus
    {
        InProgress,
        Finished,
        Error
    }

    public class TranscodeJob
    {
        public string Name { get; set; }

        public string TranscodePreset { get; set; }

        public string InputFilePath { get; set; }

        public string OutputFilePath { get; set; }

        public string Args { get; set; }

        public double Progress { get; set; }

        public string ThumbnailBase64 { get; set; }

        public string GetFullArgument ()
        {
            return $"--preset-import-file \"{TranscodePreset}\" -Z \"{Path.GetFileNameWithoutExtension(TranscodePreset)}\" -i \"{InputFilePath}\" -o \"{OutputFilePath}\"{Args}";
        }
    }
}
