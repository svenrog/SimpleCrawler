// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Crawler.Core.Robots;

/// <summary>
/// Exception raised when parsing a robots.txt file
/// </summary>
[Serializable]
public class RobotsTxtException : Exception
{
    internal RobotsTxtException()
    {
    }

    internal RobotsTxtException(string? message) : base(message)
    {
    }

    internal RobotsTxtException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    [ExcludeFromCodeCoverage]
    protected RobotsTxtException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
