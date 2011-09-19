namespace Nancy.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Nancy.Extensions;
    using Nancy.Helpers;

    /// <summary>
    /// Default implementation of a route pattern matcher.
    /// </summary>
    public class DefaultRoutePatternMatcher : IRoutePatternMatcher
    {
        private readonly ConcurrentDictionary<string, RouteSegmentsMatcher> matcherCache = new ConcurrentDictionary<string, RouteSegmentsMatcher>();

        public IRoutePatternMatchResult Match(string[] requestedPathSegments, string routePath)
        {
            var routePathPattern = this.matcherCache.GetOrAdd(routePath, (s) => new RouteSegmentsMatcher(routePath));

            DynamicDictionary parameters;
            var matched = routePathPattern.Match(requestedPathSegments, out parameters);

            return new RoutePatternMatchResult(matched, parameters);
        }

        //private static Regex BuildRegexMatcher(string path)
        //{
        //    var segments =
        //        path.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);

        //    var parameterizedSegments =
        //        GetParameterizedSegments(segments);

        //    var pattern =
        //        string.Concat(@"^/", string.Join("/", parameterizedSegments), @"$");

        //    return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        //}

        //private static DynamicDictionary GetParameters(Regex regex, GroupCollection groups)
        //{
        //    dynamic data = new DynamicDictionary();

        //    for (int i = 1; i <= groups.Count; i++)
        //    {
        //        data[regex.GroupNameFromNumber(i)] = Uri.UnescapeDataString(groups[i].Value);
        //    }

        //    return data;
        //}

        //private static IEnumerable<string> GetParameterizedSegments(IEnumerable<string> segments)
        //{
        //    foreach (var segment in segments)
        //    {
        //        var current = segment;
        //        if (current.IsParameterized())
        //        {
        //            var replacement =
        //                string.Format(CultureInfo.InvariantCulture, @"(?<{0}>(.+?))", segment.GetParameterName());

        //            current = segment.Replace(segment, replacement);
        //        }

        //        yield return current;
        //    }
        //}

        private class RouteSegmentsMatcher
        {
            private List<IRouteSegmentMatcher> segmentMatchers;

            public RouteSegmentsMatcher(string path)
            {
                segmentMatchers = new List<IRouteSegmentMatcher>();

                var segments =
                    path.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var segment in segments)
                {
                    if (segment.IsParameterized())
                    {
                        segmentMatchers.Add(new CaptureSegmentMatcher(segment));
                    }
                    else if (segment.StartsWith("(?"))
                    {
                        segmentMatchers.Add(new NamedCaptureGroupSegmentMatcher(segment));
                    }
                    else
                    {
                        segmentMatchers.Add(new LiteralSegmentMatcher(segment));
                    }
                }
            }

            public bool Match(string[] segments, out DynamicDictionary parameters)
            {
                parameters = new DynamicDictionary();

                if (segments.Length != segmentMatchers.Count)
                    return false;

                var matchResults = new List<SegmentMatchResult>();

                for (int i = 0; i < segments.Length; ++i)
                {
                    var segment = segments[i];
                    var matchResult = segmentMatchers[i].Match(segment);

                    if (!matchResult.IsMatch)
                        return false;

                    matchResults.Add(matchResult);
                }

                foreach (var matchResult in matchResults)
                {
                    foreach (var param in matchResult.Parameters)
                    {
                        parameters[param.Item1] = param.Item2;
                    }
                }

                return true;
            }
        }

        private interface IRouteSegmentMatcher
        {
            SegmentMatchResult Match(string segment);
        }

        private class LiteralSegmentMatcher : IRouteSegmentMatcher
        {
            private string literal;

            public LiteralSegmentMatcher(string segment)
            {
                this.literal = segment;
            }

            public SegmentMatchResult Match(string segment)
            {
                var matched = literal.Equals(segment);
                return new SegmentMatchResult(matched, new List<Tuple<string, string>>());
            }
        }

        private class CaptureSegmentMatcher : IRouteSegmentMatcher
        {
            private string captureName;

            public CaptureSegmentMatcher(string segment)
            {
                captureName = segment.TrimStart('{').TrimEnd('}');
            }

            public SegmentMatchResult Match(string segment)
            {
                return new SegmentMatchResult(true, new List<Tuple<string,string>> { Tuple.Create(captureName, segment) });
            }
        }

        private class NamedCaptureGroupSegmentMatcher : IRouteSegmentMatcher
        {
            private Regex regex;

            public NamedCaptureGroupSegmentMatcher(string segment)
            {
                regex = new Regex(segment, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }

            public SegmentMatchResult Match(string segment)
            {
                var match = regex.Match(segment);

                return new SegmentMatchResult(match.Success, GetParameters(match));
            }

            private List<Tuple<string, string>> GetParameters(Match match)
            {
                var paramters = new List<Tuple<string, string>>();

                var groups = match.Groups;
                for (int i = 1; i <= groups.Count; i++)
                {
                    paramters.Add(Tuple.Create(regex.GroupNameFromNumber(i), Uri.UnescapeDataString(groups[i].Value)));
                }

                return paramters;
            }
        }

        private class SegmentMatchResult
        {
            public SegmentMatchResult(bool isMatch, List<Tuple<string, string>> parameters)
            {
                IsMatch = isMatch;
                Parameters = parameters;
            }

            public bool IsMatch { get; private set; }
            public List<Tuple<string, string>> Parameters { get; private set; }
        }
    }
}