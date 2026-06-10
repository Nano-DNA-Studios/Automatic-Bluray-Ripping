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

        public TranscodeJob ()
        {
            Name = string.Empty;
            TranscodePreset = string.Empty;
            InputFilePath = string.Empty;
            OutputFilePath = string.Empty;
            Args = string.Empty;
            ThumbnailBase64 = string.Empty;
        }

        public string GetFullArgument ()
        {
            return $"--preset-import-file \"{TranscodePreset}\" -Z \"{Path.GetFileNameWithoutExtension(TranscodePreset)}\" -i \"{InputFilePath}\" -o \"{OutputFilePath}\"{Args}";
        }
    }
}
