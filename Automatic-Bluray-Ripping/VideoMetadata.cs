using MediaInfo;
using MediaInfo.Model;
using System.Diagnostics;

namespace Automatic_Bluray_Ripping
{
    public class VideoMetadata
    {
        public string FilePath { get; set; }

        public string Name { get; set; } = "";

        public string Duration { get; set; } = "";

        public VideoStreamItem[] VideoStreams { get; set; }

        public AudioStreamItem[] AudioStreams { get; set; }

        public SubtitleStreamItem[] SubtitleStreams { get; set; }

        public ChapterStreamItem[] ChapterStreams { get; set; }

        public string ThumbnailBase64 { get; set; }

        public VideoMetadata(string filePath)
        {
            FilePath = filePath;
            Name = Path.GetFileName(FilePath);
            Duration = "";

            VideoStreams = [];
            AudioStreams = [];
            SubtitleStreams = [];
            ChapterStreams = [];

            ParseFile();
        }

        private void ParseFile()
        {
            if (!File.Exists(FilePath))
                return;


            MediaInfoWrapper mediaFile = new MediaInfoWrapper(FilePath);

            mediaFile.WriteInfo();

            Duration = $"{TimeSpan.FromMilliseconds(mediaFile.Duration):hh\\:mm\\:ss}";

            List<VideoStreamItem> videoStreams = new List<VideoStreamItem>();

            foreach (var video in mediaFile.VideoStreams)
            {
                videoStreams.Add(new VideoStreamItem(video));
            }

            List<AudioStreamItem> audioStreams = new List<AudioStreamItem>();

            foreach (var audio in mediaFile.AudioStreams)
            {
                audioStreams.Add(new AudioStreamItem(audio));
            }

            List<SubtitleStreamItem> subtitleStreams = new List<SubtitleStreamItem>();

            foreach (var subtitle in mediaFile.Subtitles)
            {
                subtitleStreams.Add(new SubtitleStreamItem(subtitle));
            }

            List<ChapterStreamItem> chapterStreams = new List<ChapterStreamItem>();

            foreach (var chapter in mediaFile.Chapters)
            {
                chapterStreams.Add(new ChapterStreamItem(chapter));
            }

            VideoStreams = videoStreams.ToArray();
            AudioStreams = audioStreams.ToArray();
            SubtitleStreams = subtitleStreams.ToArray();
            ChapterStreams = chapterStreams.ToArray();

        }

        public string GetSignature(bool useVideo, bool useAudio, bool useSubtitles, bool useChapters)
        {
            string signature = string.Empty;

            if (useVideo)
            {
                foreach (var item in VideoStreams)
                    signature += item.GetSignature();
            }

            if (useAudio)
            {
                foreach (var item in AudioStreams)
                    signature += item.GetSignature();
            }

            if (useSubtitles)
            {
                foreach (var item in SubtitleStreams)
                    signature += item.GetSignature();
            }

            if (useChapters)
            {
                foreach (var item in ChapterStreams)
                    signature += item.GetSignature();
            }

            return signature;
        }

        public async Task<string> ExtractThumbnailToBase64Async()
        {
            if (!File.Exists(FilePath)) return "images/placeholder.jpg";

            string timeOffset = "00:01:00";

            try
            {
                if (TimeSpan.TryParse(this.Duration, out TimeSpan videoLength))
                {
                    if (videoLength.TotalSeconds <= 65)
                        timeOffset = "00:00:01";
                }
            }
            catch
            {
                // Fallback safety if string parsing fails
                timeOffset = "00:00:01";
            }

            // Use the dynamic timeOffset in your arguments string
            string arguments = $"-ss {timeOffset} -i \"{FilePath}\" -vframes 1 -f image2 -c:v mjpeg pipe:1";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = @"C:\FFmpeg\App\bin\ffmpeg.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();

                using (MemoryStream ms = new MemoryStream())
                {
                    await process.StandardOutput.BaseStream.CopyToAsync(ms);
                    await process.WaitForExitAsync();

                    byte[] imageBytes = ms.ToArray();

                    if (imageBytes.Length == 0 || process.ExitCode != 0)
                    {
                        Console.WriteLine($"Image Extraction failed for {Name}");
                        return "images/placeholder.jpg";
                    }

                    Console.WriteLine("Succeed");

                    return $"data:image/jpeg;base64,{Convert.ToBase64String(imageBytes)}";
                }
            }
        }
    }

    public class VideoStreamItem
    {
        public int ID { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public double FrameRate { get; set; }

        public string CodecName { get; set; }

        public string Format { get; set; }

        public VideoStreamItem(VideoStream stream)
        {
            this.ID = stream.Id;
            this.Width = stream.Width;
            this.Height = stream.Height;
            this.FrameRate = stream.FrameRate;
            this.CodecName = stream.CodecName;
            this.Format = stream.Format;
        }

        public string GetSignature()
        {
            return $"[{ID} | {Width}x{Height} | {FrameRate} | {CodecName} | {Format}]";
        }
    }

    public class AudioStreamItem
    {
        public int ID { get; set; } = 0;
        public string Format { get; set; } = "";
        public int Channels { get; set; } = 0;
        public string Language { get; set; } = "";
        public double Bitrate { get; set; } = 0;

        public AudioStreamItem(AudioStream audio)
        {
            this.ID = audio.Id;
            this.Format = $"{audio.CodecFriendly} - {audio.Name}";
            this.Channels = audio.Channel;
            this.Language = audio.Language;
            this.Bitrate = audio.Bitrate;
        }

        public string GetSignature()
        {
            return $"[{ID} | {Format} | {Channels} | {Language}]";
        }
    }

    public class SubtitleStreamItem
    {
        public int ID { get; set; } = 0;
        public string Format { get; set; } = "";
        public string Language { get; set; } = "";

        public SubtitleStreamItem(SubtitleStream subtitle)
        {
            this.ID = subtitle.Id;
            this.Format = subtitle.Format;
            this.Language = subtitle.Language;
        }

        public string GetSignature()
        {
            return $"[{ID} | {Format} | {Language}]";
        }

    }

    public class ChapterStreamItem
    {
        public int ID { get; set; } = 0;
        public string Name { get; set; } = "";
        //public string Format { get; set; } = "";
        //public string Language { get; set; } = "";

        public ChapterStreamItem(ChapterStream chapter)
        {
            this.ID = chapter.Id;
            this.Name = chapter.Name;
        }

        public string GetSignature()
        {
            return $"[{ID} | {Name}]";
        }
    }

    public class MemoryFrameExtractor
    {
        // Ensure this matches the actual execution path of your ffmpeg binary


        /// <summary>
        /// Extracts a frame entirely to memory and returns a Base64 string safe for HTML <img> tags.
        /// </summary>

    }

}
