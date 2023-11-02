// The code is under MIT-0 license <https://github.com/aws/mit-0>
using System;
using System.Threading.Tasks;
using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SavePolicy
{
    public class Function
    {
        private const string TABLE = "TABLE";
        internal static AmazonDynamoDBClient client = new AmazonDynamoDBClient();
        internal static Table table = Table.LoadTable(client, Environment.GetEnvironmentVariable(TABLE));

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {

        }


        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
        /// to respond to SQS messages.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
        {
            foreach (var message in evnt.Records)
            {
                await ProcessMessageAsync(message, context);
            }
        }

        private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            context.Logger.LogLine($"Processed message {message.Body}");

            try
            {
                var policy = JsonSerializer.Deserialize<Policy>(message.Body);
                await table.PutItemAsync(new Document {
                    { "CprNo", policy.CprNo },
                    { "PolicyOwner", policy.PolicyOwner }
                });
            }
            catch (Exception e)
            {
                context.Logger.LogLine($"ERROR: {e.Message}");
            }
            await Task.CompletedTask;
        }
    }

    public class Policy
    {
        public string CprNo { get; set; }
        public string PolicyOwner { get; set; }
    }
}
