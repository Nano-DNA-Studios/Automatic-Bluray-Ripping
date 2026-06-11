using MediaInfo.Model;

namespace Automatic_Bluray_Ripping
{
    public class VideoMetadata
    {
        public string FilePath { get; set; }

        public string Name { get; set; } = "";

        public int Duration { get; set; }

        public bool IsSelected { get; set; }

        public string ThumbnailBase64 { get; set; }

        public VideoStreamItem[] VideoStreams { get; set; }

        public AudioStreamItem[] AudioStreams { get; set; }

        public SubtitleStreamItem[] SubtitleStreams { get; set; }

        public ChapterStreamItem[] ChapterStreams { get; set; }

        public VideoMetadata(string filePath)
        {
            IsSelected = true;
            FilePath = filePath;
            Name = Path.GetFileNameWithoutExtension(FilePath);
            Duration = 0;
            ThumbnailBase64 = "file-video.svg";

            VideoStreams = [];
            AudioStreams = [];
            SubtitleStreams = [];
            ChapterStreams = [];
        }

        public string GetDuration()
        {
            return $"{TimeSpan.FromMilliseconds(Duration):hh\\:mm\\:ss}";
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

        public VideoStreamItem(VideoStream video, int duration, int id)
        {
            this.ID = id;
            this.Width = video.Width;
            this.Height = video.Height;
            this.FrameRate = video.FrameRate;
            this.CodecName = video.CodecName;
            this.Format = video.Format;
            this.Duration = $"{TimeSpan.FromMilliseconds(duration):hh\\:mm\\:ss}";
            this.IsSelected = true;
        }

        public string GetSignature() => $"[{ID} | {Width}x{Height} | {FrameRate} | {CodecName} | {Format}]";
    }

    public class AudioStreamItem
    {
        //Every Stream Has an Input + Output Subobject
        public int ID { get; set; }
        public bool IsSelected { get; set; }

        //Input Info
        public string Format { get; set; } 
        public int Channels { get; set; }
        public string Language { get; set; } 
        public double Bitrate { get; set; } 

        //Output Info
        public string Name { get; set; }
        public string Codec { get; set; } 
        public string Mixdown { get; set; } 

        public string[] AllowableMixdowns { get; set; }

        public AudioStreamItem()
        {
            this.ID = 0;
            this.IsSelected = false;
            this.Format = string.Empty;
            this.Channels = 0;
            this.Language = string.Empty;
            this.Bitrate = 0;
            this.Name = string.Empty;
            this.Codec = string.Empty;
            this.Mixdown = string.Empty;
            this.AllowableMixdowns = [];
        }

        public AudioStreamItem(AudioStream audio, int id, DefaultSettings settings)
        {
            this.ID = id;
            this.Format = $"{audio.CodecFriendly} - {audio.Name}";
            this.Channels = audio.Channel;
            this.Language = audio.Language;
            this.Bitrate = Math.Round((audio.Bitrate / 1024.0), 0);
            this.IsSelected = true;

            this.Name = $"{this.Language} - {this.Format}";
            
            this.Codec = settings.DefaultAudioCodec;
            this.Mixdown = settings.GetMaxMixdown(Channels);
            this.AllowableMixdowns = settings.GetAllowableMixdowns(Channels);
        }

        public string GetSignature() => $"[{ID} | {Format} | {Channels} | {Language}]";

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
            this.Name = "";
            this.Format = "";
            this.Language = "";
            this.IsSelected= false;
        }

        public SubtitleStreamItem(SubtitleStream subtitle, int id)
        {
            this.ID = id;
            this.Format = subtitle.Format;
            this.Language = subtitle.Language;
            this.IsSelected = true;

            this.Name = $"{Language} ({Format})";
        }

        public string GetSignature() => $"[{ID} | {Format} | {Language}]";

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

        public ChapterStreamItem(ChapterStream chapter, int id)
        {
            this.ID = id;
            this.Name = chapter.Name;
            this.IsSelected = true;
        }

        public string GetSignature() => $"[{ID} | {Name}]";
    }
}
