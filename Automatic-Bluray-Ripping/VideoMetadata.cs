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

        public bool IsSelected { get; set; }

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
            IsSelected = true;

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
                videoStreams.Add(new VideoStreamItem(video, mediaFile.Duration));
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
                        timeOffset = "00:00:02";
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

        public string GetTranscodeArgs ()
        {
            string args = "";

            //Audio
            AudioStreamItem[] selectedAudio = AudioStreams.Where(s => s.IsSelected).ToArray();

            args += $" -a " + string.Join(",", selectedAudio.Select(s => s.ID));
            args += $" -E " + string.Join(",", selectedAudio.Select(s => s.Codec));
            args += $" -6 " + string.Join(",", selectedAudio.Select(s => DefaultSettings.MixdownToCli[s.Mixdown]));
            args += $" -A " + string.Join(",", selectedAudio.Select(s => $"\"{s.Name}\""));

            SubtitleStreamItem[] selectedSubtitle = SubtitleStreams.Where(s => s.IsSelected).ToArray();

            args += $" -s " + string.Join(",", selectedSubtitle.Select(s => s.ID));
            args += $" -S " + string.Join(",", selectedSubtitle.Select(s => $"\"{s.Name}\""));
            
            return args;
        }
    }

    public class VideoStreamItem
    {
        public int ID { get; set; }
        public bool IsSelected { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public double FrameRate { get; set; }

        public string CodecName { get; set; }

        public string Format { get; set; }

        public string Duration { get; set; }


        public VideoStreamItem(VideoStream video, int duration)
        {
            this.ID = video.Id;
            this.Width = video.Width;
            this.Height = video.Height;
            this.FrameRate = video.FrameRate;
            this.CodecName = video.CodecName;
            this.Format = video.Format;
            this.Duration = $"{TimeSpan.FromMilliseconds(duration):hh\\:mm\\:ss}";
            this.IsSelected = true;
        }

        public string GetSignature()
        {
            return $"[{ID} | {Width}x{Height} | {FrameRate} | {CodecName} | {Format}]";
        }
    }

    public class AudioStreamItem
    {
        //Every Stream Has an Input + Output Subobject
        public int ID { get; set; } = 0;
        public bool IsSelected { get; set; }

        //Input Info
        public string Format { get; set; } = "";
        public int Channels { get; set; } = 0;
        public string Language { get; set; } = "";
        public double Bitrate { get; set; } = 0;

        //Output Info
        public string Name { get; set; } = "";
        public string Codec { get; set; } = "";
        public string Mixdown { get; set; } = "";

        public AudioStreamItem()
        {
            this.ID = 0;
        }

        public AudioStreamItem(AudioStream audio)
        {
            this.ID = audio.Id;
            this.Format = $"{audio.CodecFriendly} - {audio.Name}";
            this.Channels = audio.Channel;
            this.Language = audio.Language;
            this.Bitrate = Math.Round((audio.Bitrate / 1024.0), 0);
            this.IsSelected = true;

            this.Name = $"{this.Language} - {this.Format}";
            this.Codec = DefaultSettings.AudioCodec;
            this.Mixdown = DefaultSettings.GetMaxMixdown(Channels);
        }

        public string GetSignature()
        {
            return $"[{ID} | {Format} | {Channels} | {Language}]";
        }

        public void CopyOutputValues (AudioStreamItem stream)
        {
            this.Name = stream.Name;
            this.Codec = stream.Codec;
            this.Mixdown = stream.Mixdown;
        }
    }

    public class SubtitleStreamItem
    {
        public int ID { get; set; } = 0;
        public bool IsSelected { get; set; }
        
        public string Format { get; set; } = "";
        public string Language { get; set; } = "";

        //Output
        public string Name { get; set; }
        public bool BurnIn { get; set; } = false;
        public bool Default { get; set; } = false;

        public SubtitleStreamItem()
        {
            this.ID = 0;
        }

        public SubtitleStreamItem(SubtitleStream subtitle)
        {
            this.ID = subtitle.Id;
            this.Format = subtitle.Format;
            this.Language = subtitle.Language;
            this.IsSelected = true;

            this.Name = $"{Language} ({Format})";
        }

        public string GetSignature()
        {
            return $"[{ID} | {Format} | {Language}]";
        }

        public void CopyOutputValues(SubtitleStreamItem stream)
        {
            this.Name = stream.Name;
            this.BurnIn = stream.BurnIn;
            this.Default = stream.Default;
        }
    }

    public class ChapterStreamItem
    {
        public int ID { get; set; } = 0;
        public string Name { get; set; } = "";
        public bool IsSelected { get; set; }

        public ChapterStreamItem(ChapterStream chapter)
        {
            this.ID = chapter.Id;
            this.Name = chapter.Name;
            this.IsSelected = true;
        }

        public string GetSignature()
        {
            return $"[{ID} | {Name}]";
        }
    }
}
