using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.Runtime;

namespace CloudWatchAppender.Services
{
    public class ClientWrapper
    {
        private IAmazonCloudWatch _client;

        public ClientWrapper(string endPoint, string accessKey, string secret)
        {
            _endPoint = endPoint;
            _accessKey = accessKey;
            _secret = secret;

            SetupClient();
        }

        private string _endPoint;
        private string _accessKey;
        private string _secret;
        private static ConcurrentDictionary<int, Task> _tasks = new ConcurrentDictionary<int, Task>();

        public ConcurrentDictionary<int, Task> Tasks
        {
            get { return _tasks; }
        }


        private void SetupClient()
        {
            if (_client != null)
                return;

            AmazonCloudWatchConfig cloudWatchConfig = null;
            RegionEndpoint regionEndpoint = null;

            if (string.IsNullOrEmpty(_endPoint) && ConfigurationManager.AppSettings["AWSServiceEndpoint"] != null)
                _endPoint = ConfigurationManager.AppSettings["AWSServiceEndpoint"];

            if (string.IsNullOrEmpty(_accessKey) && ConfigurationManager.AppSettings["AWSAccessKey"] != null)
                _accessKey = ConfigurationManager.AppSettings["AWSAccessKey"];

            if (string.IsNullOrEmpty(_secret) && ConfigurationManager.AppSettings["AWSSecretKey"] != null)
                _secret = ConfigurationManager.AppSettings["AWSSecretKey"];

            //_client = AWSClientFactory.CreateAmazonCloudWatchClient(_accessKey, _secret);

            try
            {

                if (!string.IsNullOrEmpty(_endPoint))
                {
                    if (_endPoint.StartsWith("http"))
                    {
                        cloudWatchConfig = new AmazonCloudWatchConfig { ServiceURL = _endPoint };
                        if (string.IsNullOrEmpty(_accessKey))
                            _client = AWSClientFactory.CreateAmazonCloudWatchClient(cloudWatchConfig);
                    }
                    else
                    {
                        regionEndpoint = RegionEndpoint.GetBySystemName(_endPoint);
                        if (string.IsNullOrEmpty(_accessKey))
                            _client = AWSClientFactory.CreateAmazonCloudWatchClient(regionEndpoint);
                    }
                }
            }
            catch (AmazonServiceException)
            {
            }

            if (!string.IsNullOrEmpty(_accessKey))
                if (regionEndpoint != null)
                    _client = AWSClientFactory.CreateAmazonCloudWatchClient(_accessKey, _secret, regionEndpoint);
                else if (cloudWatchConfig != null)
                    _client = AWSClientFactory.CreateAmazonCloudWatchClient(_accessKey, _secret, cloudWatchConfig);
                else
                    _client = AWSClientFactory.CreateAmazonCloudWatchClient(_accessKey, _secret);

            //Debug
            var metricDatum = new Amazon.CloudWatch.Model.MetricDatum
                              {
                                  MetricName = "CloudWatchAppender",
                                  Value = 1,
                                  Unit = "Count"
                              };
            //_client.PutMetricData(new PutMetricDataRequest().WithNamespace("CloudWatchAppender").WithMetricData(metricDatum));
        }

        public PutMetricDataResponse PutMetricData(PutMetricDataRequest metricDataRequest)
        {
            return _client.PutMetricData(metricDataRequest);
        }

        internal void SendItOff(PutMetricDataRequest metricDataRequest)
        {
            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;

            try
            {

                var task1 =
                    Task.Factory.StartNew(() =>
                                          {
                                              var task =
                                                  Task.Factory.StartNew(() =>
                                                                        {
                                                                            try
                                                                            {
                                                                                var tmpCulture = Thread.CurrentThread.CurrentCulture;
                                                                                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-GB", false);

                                                                                System.Diagnostics.Debug.WriteLine("Sending");
                                                                                var response = PutMetricData(metricDataRequest);
                                                                                System.Diagnostics.Debug.WriteLine("RequestID: " + response.ResponseMetadata.RequestId);

                                                                                Thread.CurrentThread.CurrentCulture = tmpCulture;
                                                                            }
                                                                            catch (Exception e)
                                                                            {
                                                                                System.Diagnostics.Debug.WriteLine(e);
                                                                            }
                                                                        }, ct);

                                              try
                                              {
                                                  if (!task.Wait(30000))
                                                  {
                                                      tokenSource.Cancel();
                                                      System.Diagnostics.Debug.WriteLine(
                                                          "CloudWatchAppender timed out while submitting to CloudWatch. Exception (if any): {0}",
                                                          task.Exception);
                                                  }
                                              }
                                              catch (Exception e)
                                              {
                                                  System.Diagnostics.Debug.WriteLine(
                                                      "CloudWatchAppender encountered an error while submitting to cloudwatch. {0}", e);
                                              }
                                          });

                if (!task1.IsCompleted)
                    _tasks.TryAdd(task1.Id, task1);

                task1.ContinueWith(t =>
                                   {
                                       Task task2;
                                       _tasks.TryRemove(task1.Id, out task2);
                                       System.Diagnostics.Debug.WriteLine("Cloudwatch complete");
                                       if (task1.Exception != null)
                                           System.Diagnostics.Debug.WriteLine(string.Format("CloudWatchAppender encountered an error while submitting to CloudWatch. {0}", task1.Exception));
                                   });

            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(
                    string.Format(
                        "CloudWatchAppender encountered an error while submitting to cloudwatch. {0}", e));
            }

        }

        public static bool HasPendingRequests
        {
            get { return _tasks.Values.Any(t => !t.IsCompleted); }
        }

        public static void WaitForPendingRequests(TimeSpan timeout)
        {
            var startedTime = DateTime.UtcNow;
            var timeConsumed = TimeSpan.Zero;
            while (HasPendingRequests && timeConsumed < timeout)
            {
                Task.WaitAll(_tasks.Values.ToArray(), timeout - timeConsumed);
                timeConsumed = DateTime.UtcNow - startedTime;
            }
        }

        public static void WaitForPendingRequests()
        {
            while (HasPendingRequests)
                Task.WaitAll(_tasks.Values.ToArray());
        }

    }
}