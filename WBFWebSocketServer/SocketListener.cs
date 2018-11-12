using System;
using System.Net.Sockets;
using System.Threading;

namespace WBFWebSocketServer
{
    class SocketListener
    {
        TcpListener tcpListener;

        public void Start()
        {
            Thread thread = new Thread(Run);
            thread.Start();
        }

        public void Stop()
        {
            tcpListener.Stop();
        }

        private void Run()
        {
            tcpListener = new TcpListener(Program.Port);
            TcpClient tcpClient = default(TcpClient);

            try
            {
                tcpListener.Start();
            }
            catch (Exception)
            {
                Program.Log("Failed to start tcpListener on port " + Program.Port.ToString() + ".", 0);
                Program.Port = -1;
                return;
            }

            Program.Log("is listening for connections on port " + Program.Port.ToString() + ".", 0);
            Program.Running = true;
            Program.roomHandler.Start();

            while (Program.Running)
            {
                try
                {
                    tcpClient = tcpListener.AcceptTcpClient();
                }
                catch (Exception)//on shutdown, throws an exception.
                {

                }

                for (int i = 1; i < Program.Client.Length; i++)
                {
                    if (Program.Client[i].tcpClient.Client != null)
                    {
                        if (!Program.Client[i].tcpClient.Connected)
                        {
                            Program.Client[i].tcpClient = tcpClient;
                            if (Program.Client[i].tcpClient.Client != null)
                            {
                                Program.Client[i].clientHandler.Start((uint)i);
                            }
                            break;
                        }
                    }
                }
            }
            Program.Log("is shutting down.", 0);
        }
    }
}
