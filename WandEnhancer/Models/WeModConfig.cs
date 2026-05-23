using System.Text.Json.Serialization;

namespace WandEnhancer.Models
{
    public class WeModConfig
    {
        public string BrandName { get; set; } = null!;
        public string ExecutableName { get; set; } = null!;
        public string RootDirectory { get; set; } = null!;
        
        [JsonIgnore]
        public string ExecutablePath => System.IO.Path.Combine(RootDirectory, ExecutableName);

        public override string ToString()
        {
            return RootDirectory;
        }
    }
}