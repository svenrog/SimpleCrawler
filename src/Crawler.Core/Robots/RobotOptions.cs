namespace Crawler.Core.Robots;

public class RobotOptions
{
    /// <summary>
    /// Enables RFC3986 normalization that makes normal url input match url encoded ones.
    /// This is performance intensive.
    /// </summary>
    public bool EnableRfc3986Normalization { get; set; }

    public static readonly RobotOptions Default = new();
}
