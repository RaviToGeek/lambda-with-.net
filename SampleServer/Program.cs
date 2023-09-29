// The code is under MIT-0 license <https://github.com/aws/mit-0>
using System;
using System.Threading;

using Nancy;
using Nancy.Hosting.Self;
using RandomNameGeneratorLibrary;

namespace SampleServer
{
    public class VersionModule : NancyModule
    {
        public VersionModule()
        {
            Get("/", parameters => "Version 0.1");
        }
    }

    class Program
    {
        static PersonNameGenerator personGenerator = new PersonNameGenerator();
        static Random random = new Random();
        static void Main(string[] args)
        {
            using (var nancyHost = new NancyHost(new Uri("http://localhost:8888/")))
            {
                nancyHost.Start();

                Console.WriteLine("Nancy now listening - navigating to http://localhost:8888/. Press enter to stop");

                var timer = new Timer(SavePolicy, null, 0, 3000);
                Console.ReadKey();
                timer.Dispose();
            }

            Console.WriteLine("Stopped. Good bye!");
        }

        private static void SavePolicy(object state)
        {
            var policy = new Policy
            {
                PolicyOwner = personGenerator.GenerateRandomFirstAndLastName(),
                CprNo = GenerateCprNo()
            };
            PersistPolicy(policy);
        }

        private static string GenerateCprNo()
        {
            var daysOld = random.Next(20 * 365, 100 * 365);
            var bday = DateTime.Today.AddDays(-daysOld);
            var seq = random.Next(1000, 9999);
            return bday.ToString("ddMMyy") + "-" + seq.ToString();
        }

        private static void PersistPolicy(Policy policy)
        {
            Console.WriteLine($"CPR: {policy.CprNo} Owner: {policy.PolicyOwner}");
        }
    }

    public class Policy
    {
        public string PolicyOwner { get; set; }
        public string CprNo { get; set; }
    }
}
