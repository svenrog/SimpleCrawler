namespace Crawler.AngleSharp.Js.Models;

public readonly struct Viewport
{
    public required uint Width { get; init; }
    public required uint Height { get; init; }

    public static readonly Viewport Default = new()
    {
        Width = 1920,
        Height = 1080
    };
}
