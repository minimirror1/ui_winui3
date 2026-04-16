using System;
using System.Collections.Generic;

namespace AnimatronicsControlCenter.Core.Utilities
{
    public static class MotionFileSummary
    {
        public static int CountMotionDataFiles(IEnumerable<string> paths)
        {
            int count = 0;

            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var normalized = path.Replace('\\', '/');
                if (!normalized.StartsWith("Media/", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileNameStart = normalized.LastIndexOf('/') + 1;
                var fileName = normalized[fileNameStart..];
                if (fileName.StartsWith("MT_", StringComparison.OrdinalIgnoreCase) &&
                    fileName.EndsWith(".CSV", StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
