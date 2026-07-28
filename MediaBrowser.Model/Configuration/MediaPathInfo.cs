#pragma warning disable CS1591

namespace MediaBrowser.Model.Configuration
{
    public class MediaPathInfo
    {
        public MediaPathInfo(string path)
        {
            Path = path;
            SourceType = path.StartsWith(
                "openlist://",
                global::System.StringComparison.OrdinalIgnoreCase)
                ? MediaPathSourceType.OpenList
                : MediaPathSourceType.Local;
        }

        // Needed for xml serialization
        public MediaPathInfo()
        {
            Path = string.Empty;
        }

        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the source that owns this media path.
        /// </summary>
        public MediaPathSourceType SourceType { get; set; }
    }
}
