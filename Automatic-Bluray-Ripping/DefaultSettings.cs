namespace Automatic_Bluray_Ripping
{
    public class DefaultSettings
    {
        public static string[] AudioCodecs = { "AAC", "AC3", "TrueHD", "DTS-HD Passthru", "MP3", "FLAC 16-bit", "FLAC 24-bit", "ALAC 16-bit", "ALAC 24-bit" };

        public static string AudioCodec = "FLAC 16-bit";

        public static Dictionary<int, string> Mixdown = new Dictionary<int, string>
        {
            [1] = "Mono",
            [2] = "Stereo",
            [6] = "5.1 Surround",
            [8] = "7.1 Surround"
        };

        public static string[] GetAllowableMixdowns (int channels)
        {
            List<string> mixdowns = new();

            foreach (int key in Mixdown.Keys)
            {
                if (key <= channels)
                    mixdowns.Add(Mixdown[key]);
            }

            return mixdowns.ToArray();
        }

        public static string GetMaxMixdown (int channels)
        {
            foreach (int key in Mixdown.Keys)
                if (key == channels)
                    return Mixdown[key];

            return Mixdown[1];
        }

    }
}
