// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

namespace Crawler.Core.Robots;

/// <summary>
/// Describes a robots.txt rule for a URL
/// </summary>
/// <param name="Type">Rule type; either <see cref="RuleType.Allow"/> or <see cref="RuleType.Disallow"/></param>
/// <param name="Pattern">URL path pattern</param>
public record UrlRule(RuleType Type, UrlPathPattern Pattern);
