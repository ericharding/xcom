

using System.Collections.Specialized;
using BlackEdgeCommon.Communication.Bidirectional;

namespace xcomtest
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            string ip = "127.0.0.1";
            UInt16 port = 7675;
            if (args.Contains("--server"))
            {
                runServer(ip, port);
            }
            if (args.Contains("--client"))
            {
                runClient(ip, port);
            }
        }

        private static void runClient(string ip, ushort port)
        {
            StringAsyncClient sac = new StringAsyncClient(ip, port);
            sac.ConnectionEstablished += (o, e) => Console.WriteLine("Connection established");
            // sac.ConnectionFailed +=

        }

        private static void runServer(string ip, ushort port)
        {
            StringAsyncServer sas = new StringAsyncServer(ip, port);
            sas.NewClientIdentityConnected += (o, a) =>
            {
                Console.WriteLine($"New client {a}");
                sas.SendCommentedMessageToClients("comment?", "hello new client");
            };
            sas.NewMessageReceived += (o, a) => Console.WriteLine($"New Message: {a.Message}");


            while (true)
            {
                System.Threading.Thread.Sleep(1);
            }
        }
    }

}

