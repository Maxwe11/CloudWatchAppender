using System.IO;
using log4net.Core;
using log4net.Layout.Pattern;

namespace CloudWatchAppender
{
    internal sealed class InstanceIDPatternConverter : PatternLayoutConverter
    {
        protected override void Convert(TextWriter writer, LoggingEvent loggingEvent)
        {
            var s = AWSMetaDataReader.GetInstanceID();
            if (string.IsNullOrEmpty(s))
                writer.Write("NoInstanceID");

            writer.Write(s);
        }
    }
}