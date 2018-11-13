using System;
using System.Net.Sockets;
using System.Threading;

namespace WBFWebSocketServer
{
    class Program
    {
        public struct ClientStruct
        {
            public ClientHandler clientHandler;
            public TcpClient tcpClient;
            public Boolean Upgraded;
            public Boolean LoggedIn;
            public String InGameName;
            public String FriendCode;
            public UInt32 Room;
        }

        public static RoomHandler roomHandler;
        public static ClientStruct[] Client;
        public static UInt32 TotalClients;
        public static UInt32 TotalRooms;
        public static Int32 Port;
        public static Boolean Running;

        private static SocketListener socketListener;

        static void Main(string[] args)
        {
            roomHandler = new RoomHandler();
            Client = new ClientStruct[1024];
            Running = true;
            Port = -1;

            for (int i = 0; i < Client.Length; i++)
            {
                Client[i].clientHandler = new ClientHandler();
            }

            if (args.Length == 2)
            {
                if (args[0] == "-p" || args[0] == "-port")
                {
                    try
                    {
                        Port = Convert.ToInt32(args[1]);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Bad argument " + args[0] + " " + args[1]);
                    }
                }
            }

            Start();

            while (Running)
            {
                if (Console.ReadLine().ToLower() == "exit")
                {
                    Running = false;
                    Client = new ClientStruct[1024];
                    socketListener.Stop();
                }
                Thread.Sleep(1);
            }
        }

        private static void Start()
        {
            Running = false;
            socketListener = new SocketListener();
            while (!Running)
            {
                while (Port == -1)
                {
                    Console.WriteLine("Enter the port to run on.");
                    try
                    {
                        Port = Convert.ToInt32(Console.ReadLine());
                        if (Port < 0)
                        {
                            Console.WriteLine("Please enter a positive integer.");
                            Port = -1;
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Please enter an integer.");
                    }
                }
                socketListener.Start();
                Thread.Sleep(1000);
            }
        }

        public static void Log(String Message, UInt32 ClientID)
        {
            String Prefix;
            if (ClientID != 0x00)
            {
                Prefix = "(" + DateTime.Now + ") Client " + ClientID.ToString() + " ";
            }
            else
            {
                Prefix = "(" + DateTime.Now + ") Server ";
            }
            Console.WriteLine(Prefix + Message);
        }
    }
}