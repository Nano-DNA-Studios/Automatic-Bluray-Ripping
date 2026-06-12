namespace Automatic_Bluray_Ripping
{
    public class DefaultSettings
    {
        public string DefaultRipDirectory { get; private set; }

        public string DefaultMKVDirectory { get; private set; }

        public string DefaultTranscodeDirectory { get; private set; }

        public string DefaultAudioCodec = "flac16";
        
        public int MinVideoLength = 1;

        public DefaultSettings ()
        {
            string? exePath = Path.GetDirectoryName(Environment.ProcessPath);

            if (exePath == null)
                exePath = "./";

            DefaultRipDirectory = Path.Combine(exePath, "Rips");
            DefaultMKVDirectory = Path.Combine(exePath, "MKVs");
            DefaultTranscodeDirectory = Path.Combine(exePath, "Transcodes");

            if (!Directory.Exists(DefaultRipDirectory))
                Directory.CreateDirectory(DefaultRipDirectory);

            if (!Directory.Exists(DefaultMKVDirectory))
                Directory.CreateDirectory(DefaultMKVDirectory);

            if (!Directory.Exists(DefaultTranscodeDirectory))
                Directory.CreateDirectory(DefaultTranscodeDirectory);
        }

        public Dictionary<string, string> AudioCodecs = new Dictionary<string, string>
        {
            // Encoding Options
            { "AAC", "av_aac" },
            { "AC3", "ac3" },
            { "MP3", "mp3" },
            { "FLAC 16-bit", "flac16" },
            { "FLAC 24-bit", "flac24" },
            { "ALAC 16-bit", "alac16" },
            { "ALAC 24-bit", "alac24" },

            // Lossless / Passthrough (Copy) Options
            { "TrueHD", "copy:truehd" },
            { "DTS-HD Passthru", "copy:dtshd" },
            { "AAC Passthru", "copy:aac" },
            { "AC3 Passthru", "copy:ac3" },
            { "E-AC3 Passthru", "copy:eac3" },
            { "DTS Passthru", "copy:dts" },
            { "MP3 Passthru", "copy:mp3" },
            { "Opus Passthru", "copy:opus" },
            { "Vorbis Passthru", "copy:vorbis" },
            { "FLAC Passthru", "copy:flac" },
            { "ALAC Passthru", "copy:alac" },
            { "Auto Passthru (Global)", "copy" }
        };

        public Dictionary<int, string> Mixdown = new Dictionary<int, string>
        {
            [1] = "Mono",
            [2] = "Stereo",
            [3] = "Dolby Pro Logic II",
            [6] = "5.1 Surround",
            [7] = "6.1 Surround",
            [8] = "7.1 Surround"
        };

        // Conversion Dictionary: Maps Display Names to HandBrake CLI expected tokens (-6 / --mixdown)
        public Dictionary<string, string> MixdownToCli = new Dictionary<string, string>
        {
            { "Mono", "mono" },
            { "Stereo", "stereo" },
            { "Left Only", "left_only" },
            { "Right Only", "right_only" },
            { "Dolby Pro Logic II", "dpl2" },
            { "5.1 Surround", "5point1" },
            { "6.1 Surround", "6point1" },
            { "7.1 Surround", "7point1" }
        };

        public string[] GetAllowableMixdowns(int channels)
        {
            List<string> mixdowns = new();

            foreach (int key in Mixdown.Keys)
            {
                if (key <= channels)
                    mixdowns.Add(Mixdown[key]);
            }

            if (channels >= 2)
            {
                mixdowns.Add("Left Only");
                mixdowns.Add("Right Only");
            }

            return mixdowns.ToArray();
        }

        public string GetMaxMixdown(int channels)
        {
            foreach (int key in Mixdown.Keys)
            {
                if (key == channels)
                    return Mixdown[key];
            }

            return Mixdown[1];
        }
    }
}
