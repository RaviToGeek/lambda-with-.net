// The code is under MIT-0 license <https://github.com/aws/mit-0>
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ReadPolicy
{
    public class Function
    {
        const string TABLE = "TABLE";
        private static AmazonDynamoDBClient dynamoClient = new AmazonDynamoDBClient();
        private static AmazonLambdaClient lambdaClient;

        static Function()
        {
            initialize();
        }

        static async void initialize()
        {
            lambdaClient = new AmazonLambdaClient();
            await callLambda();
        }

        public static async Task<GetAccountSettingsResponse> callLambda()
        {
            var request = new GetAccountSettingsRequest();
            var response = await lambdaClient.GetAccountSettingsAsync(request);
            return response;
        }

        /// <summary>
        /// Search for a policy with a given CPR.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns>Policy JSON</returns>
        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest apigProxyEvent, ILambdaContext context)
        {
            if (!apigProxyEvent.PathParameters.ContainsKey("cprno"))
            {
                return new APIGatewayProxyResponse
                {
                    Body = "Wrong request",
                    StatusCode = 400,
                };
            }
            var cprNo = Uri.UnescapeDataString(apigProxyEvent.PathParameters["cprno"]);
            context.Logger.LogLine($"Searching for {cprNo}");
            try
            {
                var response = await dynamoClient.QueryAsync(new QueryRequest()
                {
                    TableName = System.Environment.GetEnvironmentVariable(TABLE),
                    KeyConditionExpression = "CprNo = :v_Cpr",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                        {":v_Cpr", new AttributeValue { S = cprNo }}
                    },
                    Limit = 1
                });
                var responseValue = response.Items.FirstOrDefault();
                if (responseValue == null)
                {
                    return new APIGatewayProxyResponse
                    {
                        Body = "Not found",
                        StatusCode = 404,
                    };
                }

                return new APIGatewayProxyResponse
                {
                    Body = FormatPolicy(responseValue),
                    StatusCode = 200
                };
            }
            catch (Exception ex)
            {
                context.Logger.LogLine(ex.Message);
                return new APIGatewayProxyResponse
                {
                    Body = apigProxyEvent.Body,
                    StatusCode = 409
                };
            }
        }

        private string FormatPolicy(Dictionary<string, AttributeValue> responseValue)
        {
            var result = new Policy
            {
                CprNo = responseValue["CprNo"].S,
                PolicyOwner = responseValue["PolicyOwner"].S
            };
            return JsonSerializer.Serialize(result);
        }
    }

    public class Policy
    {
        public string CprNo { get; set; }
        public string PolicyOwner { get; set; }
    }
}
