using System;

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
                Task.Factory.StartNew(() =>
                {
                    runServer(ip, port);
                });
            }
            if (args.Contains("--client"))
            {
                Task.Factory.StartNew(() =>
                {
                    runClient(ip, port);
                });
            }
            sleepForever();
        }

        private static void sleepForever()
        {
            while (true)
            {
                Thread.Sleep(1);
            }
        }

        private static async Task runClient(string ip, ushort port)
        {
            StringAsyncClient sac = new StringAsyncClient(ip, port);
            sac.ConnectionEstablished += (o, e) => Console.WriteLine("Connection established");
            sac.Disconnected += (o, e) => Console.WriteLine($"Disconnected {e}");
            sac.NewMessageReceived += (o, a) => Console.WriteLine($"New message: {a.Message}");
            while (true) { await Task.Delay(100); }
        }

        private static async Task runServer(string ip, ushort port)
        {
            StringAsyncServer sas = new StringAsyncServer(ip, port);
            sas.NewClientIdentityConnected += async (o, a) =>
            {
                Console.WriteLine($"** New client {a}");
                while(true) {
                    await Task.Delay(1000);
                    Console.WriteLine("Print...");
                    sas.SendCommentedMessageToClients("comment?", "hello new client");
                }
            };
            sas.NewMessageReceived += (o, a) => Console.WriteLine($"New Message: {a.Message}");
            Console.WriteLine($"Started server on {ip}:{port}");
            while (true) { await Task.Delay(100); }
        }
    }

}

