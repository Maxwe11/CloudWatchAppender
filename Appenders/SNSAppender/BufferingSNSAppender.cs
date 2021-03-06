﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using AWSAppender.Core;
using AWSAppender.Core.Layout;
using AWSAppender.Core.Services;
using AWSAppender.SNS.Model;
using AWSAppender.SNS.Parsers;
using AWSAppender.SNS.Services;
using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;
using log4net.Util;

namespace AWSAppender.SNS
{
    public class BufferingSNSAppender : BufferingAWSAppenderBase<SNSDatum>, ISNSAppender
    {
        private EventRateLimiter _eventRateLimiter = new EventRateLimiter();
        private SNSClientWrapper _client;
        private static readonly Type _declaringType = typeof(BufferingSNSAppender);
        private static string _fallbackTopic;

        private string _topic;
        private string _message;

        private IEventProcessor<SNSDatum> _eventProcessor;

        private bool _configOverrides = true;

        private AmazonSimpleNotificationServiceConfig _clientConfig;
        private string _delaySeconds;

        public override IEventProcessor<SNSDatum> EventProcessor
        {
            get { return _eventProcessor; }
            set { _eventProcessor = value; }
        }

        protected override ClientConfig ClientConfig
        {
            get { return _clientConfig ?? (_clientConfig = new AmazonSimpleNotificationServiceConfig()); }
        }

        protected override void ResetClient()
        {
            _client = null;
        }

        public string Topic
        {
            set
            {
                _topic = value;
                _eventProcessor = null;
            }
            get { return _topic; }
        }

        public string DelaySeconds
        {
            set
            {
                _delaySeconds = value;
                _eventProcessor = null;
            }
            get { return _delaySeconds; }
        }



        public string Message
        {
            set
            {
                _message = value;
                _eventProcessor = null;
            }
            get { return _message; }
        }


        public new bool ConfigOverrides
        {
            set
            {
                _configOverrides = value;
                _eventProcessor = null;
            }
        }

        public BufferingSNSAppender()
        {
            _fallbackTopic = "unspecified";
            if (Assembly.GetEntryAssembly() != null)
                _fallbackTopic = Assembly.GetEntryAssembly().GetName().Name;

            var hierarchy = ((Hierarchy)LogManager.GetRepository());
            var logger = hierarchy.GetLogger("Amazon") as Logger;
            logger.Level = Level.Off;

            hierarchy.AddRenderer(typeof(SNSDatum), new SNSDatumRenderer());


        }

        public override void ActivateOptions()
        {
            base.ActivateOptions();

            EventMessageParser = EventMessageParser ?? new SNSMessageParser(_configOverrides);

            _client = new SNSClientWrapper(EndPoint, AccessKey, Secret, ClientConfig);

            _eventProcessor = new SNSEventProcessor(_topic, _message/*,_delaySeconds*/)
                              {
                                  EventMessageParser = EventMessageParser
                              };


            if (Layout == null)
                Layout = new PatternLayout("%message");

        }


        protected override void Append(LoggingEvent loggingEvent)
        {
            if (!_eventRateLimiter.Request(loggingEvent.TimeStamp))
            {
                LogLog.Debug(_declaringType, "Appending denied due to event limiter saturated.");
            }
            else
            {
                base.Append(loggingEvent);
            }

        }

        protected override void SendBuffer(LoggingEvent[] events)
        {
            var rs = ProcessEvents(events);

            var requests = Assemble(rs);

            foreach (var putMetricDataRequest in requests)
                _client.AddPublishRequest(putMetricDataRequest);
        }

        protected virtual IEnumerable<SNSDatum> ProcessEvents(LoggingEvent[] events)
        {
            return events.SelectMany(e => _eventProcessor.ProcessEvent(e, RenderLoggingEvent(e)).Select(r => r));
        }

        private static IEnumerable<PublishRequestWrapper> Assemble(IEnumerable<SNSDatum> data)
        {

            if (data.Any(x => Encoding.UTF8.GetByteCount(x.Message) > 256 * 1024))
                throw new MessageTooLargeException();

            return data.Select(snsDatum => new PublishRequestWrapper
                                           {
                                               Message = snsDatum.Message,
                                               Topic = snsDatum.Topic ?? _fallbackTopic
                                           });
        }
    }

    internal class MessageTooLargeException : Exception
    {
    }
}




