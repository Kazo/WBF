using System;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace WBFWebSocketServer
{
    class ClientHandler
    {
        UInt32 ClientID;

        public void Start(UInt32 clientID)
        {
            ClientID = clientID;
            Thread thread;
            thread = new Thread(Run);
            thread.Start();
        }

        private void Run()
        {
            try
            {
                while (Program.Client[ClientID].tcpClient != null)
                {
                    while (Program.Client[ClientID].tcpClient.Available == 0x00)
                    {
                        if (!Program.Client[ClientID].tcpClient.Connected)
                        {
                            DisconnectClient("timed out.", ClientID);
                            return;
                        }
                        Thread.Sleep(1);
                    }

                    Byte[] Reply = new Byte[Program.Client[ClientID].tcpClient.Available];

                    Read(ClientID, Reply);

                    if (Reply[0x00] == 0x88)
                    {
                        DisconnectClient("left.", ClientID);
                        return;
                    }

                    if (new Regex("^GET").IsMatch(Encoding.UTF8.GetString(Reply)))
                    {
                        PromoteClient(Reply);
                    }
                    else
                    {
                        ProcessReply(Reply);
                    }
                    Thread.Sleep(1);
                }
            }
            catch(Exception)
            {
                DisconnectClient("timed out.", ClientID);
                return;
            }
        }

        private void Read(UInt32 Client, Byte[] Reply)
        {
            try
            {
                Program.Client[Client].tcpClient.GetStream().Read(Reply, 0, Reply.Length);
            }
            catch (Exception)
            {
                DisconnectClient("timed out.", Client);
            }
        }

        private void Write(UInt32 Client, Byte[] Response)
        {
            try
            {
                Program.Client[Client].tcpClient.GetStream().Write(Response, 0, Response.Length);
            }
            catch (Exception)
            {
                DisconnectClient("timed out.", Client);
            }
        }

        public void SendMessage(UInt32 Client, Byte[] Response)
        {
            Write(Client, Response);
        }

        private void PromoteClient(Byte[] Reply)
        {
            Byte[] Response = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + Environment.NewLine + "Connection: Upgrade" + Environment.NewLine + "Upgrade: websocket" + Environment.NewLine + "Sec-WebSocket-Accept: " + Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(new Regex("Sec-WebSocket-Key: (.*)").Match(Encoding.UTF8.GetString(Reply)).Groups[1].Value.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"))) + Environment.NewLine + Environment.NewLine);
            Write(ClientID, Response);
        }

        private void ProcessReply(Byte[] Reply)
        {
            if (Reply[0x00] == 0x81)
            {
                String[] msg = SanitizedText(DecryptMessage(Reply)).Split('\n');

                switch (msg[0])
                {
                    case "0"://Connect
                        {
                            Program.Client[ClientID].Upgraded = true;
                            SendStats(ClientID);
                        }
                        break;

                    case "1"://Login
                        {
                            Program.Client[ClientID].LoggedIn = true;
                            Program.TotalClients++;
                            Program.Client[ClientID].InGameName = msg[1];
                            Program.Client[ClientID].FriendCode = msg[2];
                            Program.Log("joined.", ClientID);
                            Program.roomHandler.UpdateRooms(ClientID);
                            SendStatsAll();
                        }
                        break;

                    case "2":///Make Room
                        {
                            Program.roomHandler.MakeRoom(msg[1], ClientID);
                            Program.Log("has joined room " + Program.Client[ClientID].Room.ToString() + ".", ClientID);
                        }
                        break;

                    case "3"://Join Room
                        {
                            if (Program.roomHandler.JoinRoom(Convert.ToUInt32(msg[1]), ClientID))
                            {
                                Program.Log("has joined room " + Program.Client[ClientID].Room.ToString() + ".", ClientID);

                                SendStatsAll();
                                Program.roomHandler.UpdateRoomsAll(ClientID);
                            }
                        }
                        break;

                    case "4"://Leave Room
                        {
                            Program.Log("has left room " + Program.Client[ClientID].Room.ToString() + ".", ClientID);
                            Program.roomHandler.LeaveRoom(ClientID);
                        }
                        break;

                    case "9"://Chat
                        {
                            if (msg[1] != "")
                            {
                                msg[1] = ConvertUrlsToLinks(msg[1]);
                                Program.roomHandler.SendMessage(msg[1], ClientID);
                            }
                        }
                        break;
                }
            }
        }

        private String DecryptMessage(Byte[] Reply)
        {
            UInt64 Length = (ulong)(Reply[0x01] & 0x7F);
            Byte[] XORKeys = new Byte[4];
            UInt32 DataOffset = 0x00;
            switch (Length)
            {
                case 0x7E:
                    {
                        Length = ReverseEndianness16(BitConverter.ToUInt16(Reply, 0x02));
                        XORKeys[0x00] = Reply[0x04];
                        XORKeys[0x01] = Reply[0x05];
                        XORKeys[0x02] = Reply[0x06];
                        XORKeys[0x03] = Reply[0x07];
                        DataOffset = 0x08;
                    }
                    break;

                case 0x7F:
                    {
                        Length = ReverseEndianness64(BitConverter.ToUInt64(Reply, 0x02));
                        XORKeys[0x00] = Reply[0x0A];
                        XORKeys[0x01] = Reply[0x0B];
                        XORKeys[0x02] = Reply[0x0C];
                        XORKeys[0x03] = Reply[0x0D];
                        DataOffset = 0x0E;
                    }
                    break;

                default:
                    {
                        XORKeys[0x00] = Reply[0x02];
                        XORKeys[0x01] = Reply[0x03];
                        XORKeys[0x02] = Reply[0x04];
                        XORKeys[0x03] = Reply[0x05];
                        DataOffset = 0x06;
                    }
                    break;
            }

            Byte[] DecryptedData = new Byte[Length];
            for (int i = 0; i < DecryptedData.Length; i++)
            {
                DecryptedData[i] = (byte)(Reply[DataOffset + i] ^ XORKeys[i % 4]);
            }
            return Encoding.UTF8.GetString(DecryptedData); /*Regex.Replace(Encoding.UTF8.GetString(DecryptedData), @"<(.|\n)*?>", "");*/
        }

        private static UInt16 ReverseEndianness16(UInt16 value)
        {
            return (UInt16)((value & 0xFF) << 8 | (value & 0xFF00) >> 8);
        }

        private static UInt64 ReverseEndianness64(UInt64 value)
        {
            return (value & 0x00000000000000FF) << 56 | (value & 0x000000000000FF00) << 40 | (value & 0x0000000000FF0000) << 24 | (value & 0x00000000FF000000) << 8 | (value & 0x000000FF00000000) >> 8 | (value & 0x0000FF0000000000) >> 24 | (value & 0x00FF000000000000) >> 40 | (value & 0xFF00000000000000) >> 56;
        }

        private string ConvertUrlsToLinks(string msg)
        {
            string regex = @"((www\.|(http|https|ftp|news|file)+\:\/\/)[&#95;.a-z0-9-]+\.[a-z0-9\/&#95;:@=.+?,##%&~-]*[^.|\'|\# |!|\(|?|,| |>|<|;|\)])";
            Regex r = new Regex(regex, RegexOptions.IgnoreCase);
            return r.Replace(msg, "<a href=\"$1\" target=\"_blank\">$1</a>").Replace("href=\"www", "href=\"http://www");
        }

        public void SendStatsAll()
        {
            for (uint i = 1; i < Program.Client.Length; i++)
            {
                SendStats(i);
            }
        }

        private void SendStats(UInt32 Client)
        {
            SendCommand(Client, "0\n" + Program.TotalClients.ToString() + "\n" + Program.TotalRooms.ToString());
        }

        public void SendCommand(UInt32 Client, String Command)
        {
            if (Program.Client[Client].Upgraded)
            {
                Byte[] Response = Encoding.UTF8.GetBytes("XX" + Command);
                Response[0] = 0x81;
                Response[1] = (byte)(Response.Length - 2);
                Write(Client, Response);
            }
        }

        public void DisconnectClient(String Message, UInt32 Client)
        {
            if (Program.Client[Client].LoggedIn)
            {
                if (Program.Client[Client].Room != 0)
                {
                    Program.roomHandler.LeaveRoom(Client);
                }
                if (Program.TotalClients > 0)
                {
                    Program.TotalClients--;
                }
                Program.Log(Message, Client);
                Program.Client[Client].LoggedIn = false;
                Program.Client[Client].Upgraded = false;
                SendStatsAll();
            }
            //Program.Client[Client].tcpClient.Close();
            Program.Client[Client] = new Program.ClientStruct();
            Program.Client[Client].clientHandler = new ClientHandler();
        }

        public String SanitizedText(String Message)
        {
            return Message.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
