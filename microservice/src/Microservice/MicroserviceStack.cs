using Amazon.CDK;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.SSM;
using Constructs;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microservice
{
  public class MicroserviceStack : Stack
  {
    const string QUEUE_SSM_PARAMETER_NAME = "/monolith/policyQueueUrl";
    private readonly string unique = "rbhalala";

    internal MicroserviceStack(Construct scope, string id, string unique, IStackProps props = null)
      : base(scope, id, props)
    {
      this.unique = unique;

      var queue = new Queue(this, Id("queue"), new QueueProps
      {
        Fifo = true
      });

      var ssmParam = new StringParameter(this, Id("policyQueueUrl"), new StringParameterProps
      {
        ParameterName = Id(QUEUE_SSM_PARAMETER_NAME),
        Description = "URL of the queue with policies",
        StringValue = queue.QueueUrl
      });

      var table = new Table(this, Id("policies"), new TableProps
      {
        TableName = Id("policies"),
        PartitionKey = new Attribute
        {
          Name = "CprNo",
          Type = AttributeType.STRING
        },
        SortKey = new Attribute
        {
          Name = "PolicyOwner",
          Type = AttributeType.STRING
        }
      });

      var savePolicy = new Function(this, Id("savePolicy"), new FunctionProps
      {
        Runtime = Runtime.DOTNET_6,
        FunctionName = Id("savePolicy"),
        Handler = "SavePolicy::SavePolicy.Function::FunctionHandler",
        // dotnet
        //Code = Code.FromAsset(GetAssetPath("src/lambdas/SavePolicy/src/SavePolicy/bin/Debug/net6.0")),
        // VS2019/VS2022
        Code = Code.FromAsset(GetAssetPath("src/lambdas/SavePolicy/bin/Debug/net6.0")),
        Timeout = Duration.Seconds(15),
        Environment = new Dictionary<string, string>
        {
          ["TABLE"] = table.TableName
        },
        Events = new[] { new SqsEventSource(queue) }
      });
      table.GrantWriteData(savePolicy);
      table.Grant(savePolicy, "dynamodb:DescribeTable");

      var readPolicy = new Function(this, Id("readPolicy"), new FunctionProps
      {
        Runtime = Runtime.DOTNET_6,
        FunctionName = Id("readPolicy"),
        Handler = "ReadPolicy::ReadPolicy.Function::FunctionHandler",
        // dotnet
        //Code = Code.FromAsset(GetAssetPath("src/lambdas/ReadPolicy/src/ReadPolicy/bin/Debug/net6.0")),
        // VS2019/VS2020
        Code = Code.FromAsset(GetAssetPath("src/lambdas/ReadPolicy/bin/Debug/net6.0")),
        Timeout = Duration.Seconds(15),
        Environment = new Dictionary<string, string>
        {
          ["TABLE"] = table.TableName
        },
      });
      table.GrantReadData(readPolicy);
      table.Grant(readPolicy, "dynamodb:DescribeTable");
      // needed for X-Ray
      readPolicy.AddToRolePolicy(new Amazon.CDK.AWS.IAM.PolicyStatement(
          new Amazon.CDK.AWS.IAM.PolicyStatementProps()
          {
            Effect = Amazon.CDK.AWS.IAM.Effect.ALLOW,
            Actions = new[] { "lambda:GetAccountSettings" },
            Resources = new[] { "*" }
          }));

      var api = new LambdaRestApi(this, Id("PoliciesAPI"), new LambdaRestApiProps
      {
        Handler = readPolicy,
      });
      // see https://docs.aws.amazon.com/cdk/api/latest/docs/aws-apigateway-readme.html
      var apiPolicies = api.Root.AddResource("policies");
      var apiPolicy = apiPolicies.AddResource("{cprno}");
      apiPolicy.AddMethod("GET");

      new CfnOutput(this, "Queue", new CfnOutputProps() { Value = queue.QueueUrl });
      new CfnOutput(this, "Api", new CfnOutputProps() { Value = api.Url });
    }

    private string Id(string id)
    {
      return id + unique;
    }

    private string GetAssetPath(string path)
    {
      return Debugger.IsAttached
          ? "../../../../../" + path
          : path;
    }
  }
}
