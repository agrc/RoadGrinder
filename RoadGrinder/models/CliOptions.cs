namespace RoadGrinder.models
{
    public class CliOptions
    {
        public string SdeConnectionPath { get; set; }
        public string OutputGeodatabase { get; set; }
        public OutputType OutputType { get; set; }
        public bool Verbose { get; set; }
    }
}