using System;
using System.Linq;
using Amazon.CloudWatch;
using CloudWatchAppender.Parsers;
using NUnit.Framework;

namespace CloudWatchAppender.Tests
{
    [TestFixture]
    public class LogsEventParserTests
    {
        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void SingleValueAndUnit()
        {
            var parser = new MetricDatumEventMessageParser();
            for (int i = 0; i < 2; i++)
            {
                parser.Parse("A tick! Value: 3.0 Kilobytes/Second");

                var passes = 0;
                foreach (var r in parser.GetParsedData())
                {
                    Assert.AreEqual(StandardUnit.KilobytesSecond, r.MetricData[0].Unit);
                    Assert.AreEqual(3.0, r.MetricData[0].Value);
                    passes++;
                }

                Assert.AreEqual(1, passes);
            }
        }

        [Test]
        public void SingleValueAndUnit_Overrides()
        {
            var parser = new MetricDatumEventMessageParser()
                         {
                             DefaultValue = 4.0,
                             DefaultUnit = "Megabytes/Second"
                         };

            for (int i = 0; i < 2; i++)
            {
                parser.Parse("A tick! Value: 3.0 Kilobytes/Second");

                var passes = 0;
                foreach (var r in parser.GetParsedData())
                {
                    Assert.AreEqual(StandardUnit.MegabytesSecond, r.MetricData[0].Unit);
                    Assert.AreEqual(4.0, r.MetricData[0].Value);
                    passes++;
                }

                Assert.AreEqual(1, passes);
            }
        }


        [Test]
        public void NothingRecognizableShouldProduceCount1()
        {
            var parser = new LogsEventMessageParser();

            for (int i = 0; i < 2; i++)
            {
                parser.Parse("A tick");

                var passes = 0;
                foreach (var r in parser.GetParsedData())
                {
                    Assert.AreEqual("unspecified", r.StreamName);
                    Assert.AreEqual("A tick", r.Message);
                    Assert.AreEqual(null, r.Timestamp);
                    Assert.AreEqual("unspecified", r.GroupName);

                    passes++;
                }
                Assert.AreEqual(1, passes);
            }
        }

        [Test]
        public void TrailingNames()
        {
            var parser = new LogsEventMessageParser();
            for (int i = 0; i < 2; i++)
            {
                parser.Parse("A tick! StreamName: NewName GroupName: GName");

                var data = parser.GetParsedData();

                Assert.That(data.Count(), Is.EqualTo(1));
                Assert.That(data.Select(x => x.StreamName), Has.All.EqualTo("NewName"));
                Assert.That(data.Select(x => x.GroupName), Has.All.EqualTo("GName"));
                Assert.That(data.Select(x => x.Message), Has.All.EqualTo("A tick!"));
            }
        }

        [Test]
        public void LeadingNames()
        {
            var parser = new LogsEventMessageParser();
            for (int i = 0; i < 2; i++)
            {
                parser.Parse("StreamName: NewName GroupName: GName A tick!");

                var data = parser.GetParsedData();

                Assert.That(data.Count(), Is.EqualTo(1));
                Assert.That(data.Select(x => x.StreamName), Has.All.EqualTo("NewName"));
                Assert.That(data.Select(x => x.GroupName), Has.All.EqualTo("GName"));
                Assert.That(data.Select(x => x.Message), Has.All.EqualTo("A tick!"));
            }
        }

        [Test]
        public void SurroundingingNames()
        {
            var parser = new LogsEventMessageParser();
            for (int i = 0; i < 2; i++)
            {
                parser.Parse("StreamName: NewName A tick! GroupName: GName");

                var data = parser.GetParsedData();

                Assert.That(data.Count(), Is.EqualTo(1));
                Assert.That(data.Select(x => x.StreamName), Has.All.EqualTo("NewName"));
                Assert.That(data.Select(x => x.GroupName), Has.All.EqualTo("GName"));
                Assert.That(data.Select(x => x.Message), Has.All.EqualTo("A tick!"));
            }
        }

        [Test]
        public void SurroundedNames()
        {
            var parser = new LogsEventMessageParser();
            for (int i = 0; i < 2; i++)
            {
                parser.Parse("Beginning tick! StreamName: NewName Middle tick! GroupName: GName End tick!");

                var data = parser.GetParsedData();

                Assert.That(data.Count(), Is.EqualTo(1));
                Assert.That(data.Select(x => x.StreamName), Has.All.EqualTo("NewName"));
                Assert.That(data.Select(x => x.GroupName), Has.All.EqualTo("GName"));
                Assert.That(data.Select(x => x.Message), Has.All.EqualTo("Beginning tick! Middle tick! End tick!"));
            }
        }

        [Test]
        public void ParenthesizedNames()
        {
            var parser = new LogsEventMessageParser();
            for (int i = 0; i < 2; i++)
            {
                parser.Parse("StreamName: (New Name) A tick! GroupName: GName");

                var data = parser.GetParsedData();

                Assert.That(data.Count(), Is.EqualTo(1));
                Assert.That(data.Select(x => x.StreamName), Has.All.EqualTo("New Name"));
                Assert.That(data.Select(x => x.GroupName), Has.All.EqualTo("GName"));
                Assert.That(data.Select(x => x.Message), Has.All.EqualTo("A tick!"));
            }
        }


        [Test]
        public void Timestamp()
        {
            var parser = new LogsEventMessageParser();
            for (int i = 0; i < 2; i++)
            {
                parser.Parse("A tick! Timestamp: 2012-09-06 17:55:55 +02:00");
                var data = parser.GetParsedData();

                Assert.That(data.Count(), Is.EqualTo(1));
                Assert.That(data.Select(x => x.Timestamp), Has.All.EqualTo(DateTime.Parse("2012-09-06 17:55:55")));
                Assert.That(data.Select(x => x.Message), Has.All.EqualTo("A tick!"));
            }

            parser = new LogsEventMessageParser();
            for (int i = 0; i < 2; i++)
            {
                parser.Parse("A tick! Timestamp: 2012-09-06 15:55:55");
                var data = parser.GetParsedData();

                Assert.That(data.Count(), Is.EqualTo(1));
                Assert.That(data.Select(x => x.Timestamp), Has.All.EqualTo(DateTime.Parse("2012-09-06 15:55:55")));
                Assert.That(data.Select(x => x.Message), Has.All.EqualTo("A tick!"));
            }
        }


        [Test]
        //[Ignore("Ignore until App Veyor deploy to nuget is working")]
        public void Timestamp_Override()
        {
            var parser = new LogsEventMessageParser()
                         {
                             DefaultTimestamp = DateTime.Parse("2012-09-06 12:55:55 +02:00")
                         };

            for (int i = 0; i < 2; i++)
            {
                parser.Parse("A tick! Timestamp: 2012-09-06 17:55:55 +02:00");
                var data = parser.GetParsedData();

                Assert.That(data.Count(), Is.EqualTo(1));
                Assert.That(data.Select(x => x.Timestamp), Has.All.EqualTo(DateTime.Parse("2012-09-06 12:55:55")));
                Assert.That(data.Select(x => x.Message), Has.All.EqualTo("A tick!"));
            }
        }
    }
}