using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nancy.Tests.Unit.Routing
{
    public static class RoutingExtensions
    {
        public static string[] SplitUrl(this string url)
        {
            return url.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
