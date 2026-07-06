namespace SimpleCrawler.Core.Robots;

public readonly record struct RobotResourceResponse(int Status, byte[]? Body, string? MediaType);
