// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

namespace Crawler.Core.Robots;

/// <summary>
/// Provides the ability to check accessibility of URLs for a robot
/// </summary>
public interface IRobotRuleChecker
{
    /// <summary>
    /// Checks if the robot is allowed to access the path
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>True if the robots is allowed to access the path; otherwise false</returns>
    bool IsAllowed(string path);
}