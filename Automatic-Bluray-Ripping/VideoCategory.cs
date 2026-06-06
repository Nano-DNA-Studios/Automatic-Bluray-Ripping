namespace Automatic_Bluray_Ripping
{
    public class VideoCategory
    {
        public string Name { get; private set; }

        public bool IsSelected { get; set; }

        public List<VideoMetadata> Metadata { get; private set; }

        public VideoCategory(string name)
        {
            this.Name = name;
            this.IsSelected = true;
            Metadata = new List<VideoMetadata>();
        }

        public void AddMetadata(VideoMetadata metadata)
        {
            Metadata.Add(metadata);
        }
    }
}
