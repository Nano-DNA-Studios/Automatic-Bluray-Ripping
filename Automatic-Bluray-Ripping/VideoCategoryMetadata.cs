namespace Automatic_Bluray_Ripping
{
    public class VideoCategoryMetadata
    {
        public string Name { get; set; }

        public string NamingPattern { get; set; }

        public bool IsSelected { get; set; }

        public List<VideoMetadata> Metadata { get; private set; }

        public VideoStreamItem[] VideoStreams { get; set; }

        public AudioStreamItem[] AudioStreams { get; set; }

        public SubtitleStreamItem[] SubtitleStreams { get; set; }

        public ChapterStreamItem[] ChapterStreams { get; set; }

        public VideoCategoryMetadata(string name)
        {
            this.Name = name;
            this.IsSelected = true;
            Metadata = new List<VideoMetadata>();
        }

        public void AddMetadata(VideoMetadata metadata)
        {
            if (Metadata.Count == 0)
            {
                this.VideoStreams = metadata.VideoStreams;
                this.AudioStreams = metadata.AudioStreams;
                this.SubtitleStreams = metadata.SubtitleStreams;
                this.ChapterStreams = metadata.ChapterStreams;
            }

            Metadata.Add(metadata);
        }

        public void ApplyStreamSelection ()
        {
            foreach(VideoMetadata metadata in Metadata)
            {
                for (int i = 0; i < VideoStreams.Length; i ++)
                    metadata.VideoStreams[i].IsSelected = VideoStreams[i].IsSelected;

                for (int i = 0; i < AudioStreams.Length; i++)
                    metadata.AudioStreams[i].IsSelected = AudioStreams[i].IsSelected;

                for (int i = 0; i < SubtitleStreams.Length; i++)
                    metadata.SubtitleStreams[i].IsSelected = SubtitleStreams[i].IsSelected;

                for (int i = 0; i < ChapterStreams.Length; i++)
                    metadata.ChapterStreams[i].IsSelected = ChapterStreams[i].IsSelected;
            }
        }

        public void ApplyOutputConfig ()
        {
            ApplyStreamSelection();

            foreach (VideoMetadata metadata in Metadata)
            {
                for (int i = 0; i < AudioStreams.Length; i++)
                    metadata.AudioStreams[i].CopyOutputValues(AudioStreams[i]);

                for (int i = 0; i < SubtitleStreams.Length; i++)
                    metadata.SubtitleStreams[i].CopyOutputValues(SubtitleStreams[i]);
            }

            if (string.IsNullOrEmpty(NamingPattern))
                return;

            for (int i = 0; i < Metadata.Count; i++)
                Metadata[i].Name = NamingPattern + (i + 1);
        }
    }
}
