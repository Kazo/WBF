using System;
using System.Collections.Generic;
using System.Text;
//using System.Threading;

namespace WBFWebSocketServer
{
    class RoomHandler
    {
        public struct RoomStruct
        {
            public UInt32[] Members;
            public UInt32 Size;
            public String Name;
        }

        RoomStruct[] Rooms;

        public void Start()
        {
            Rooms = new RoomStruct[1024];
            for(int i = 0; i < Rooms.Length; i++)
            {
                Rooms[i].Members = new UInt32[4];
            }

            /*Thread thread = new Thread(Run);
            thread.Start();*/
        }

        /*private void Run()
        {
            while (Program.Running)
            {
                Thread.Sleep(1);
            }
        }*/

        public void MakeRoom(String Name, UInt32 ClientID)
        {
            for (uint i = 1; i < Rooms.Length; i++)
            {
                if ((Rooms[i].Members[0] == 0))
                {
                    Rooms[i].Members[0] = ClientID;
                    Rooms[i].Size++;
                    Rooms[i].Name = Name;
                    Program.Client[ClientID].Room = i;
                    Program.TotalRooms++;
                    Program.Client[ClientID].clientHandler.SendStatsAll();
                    UpdateRoomsAll(ClientID);
                    Program.Log("has opened room " + i.ToString() + ".", 0);
                    break;
                }
            }
        }

        public Boolean JoinRoom(UInt32 Room, UInt32 ClientID)
        {
            if (Program.Client[ClientID].Room != 0)
            {
                LeaveRoom(ClientID);
            }

            for (int i = 0; i < Rooms[Room].Members.Length; i++)
            {
                if (Rooms[Room].Members[i] == 0)
                {
                    Rooms[Room].Members[i] = ClientID;
                    Rooms[Room].Size++;
                    Program.Client[ClientID].Room = Room;
                    foreach(UInt32 Client in Rooms[Room].Members)
                    {
                        foreach (UInt32 Member in Rooms[Room].Members)
                        {
                            if ((Client != Member) && (Member != 0))
                            {
                                Program.Client[ClientID].clientHandler.SendCommand(Client, "2\n" + Program.Client[Member].InGameName + "\n" + Program.Client[Member].FriendCode + "\n" + Rooms[Room].Name);
                            }
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        public void UpdateRoomsAll(UInt32 ClientID)
        {
            for (uint i = 1; i < Program.Client.Length; i++)
            {
                if (Program.Client[i].LoggedIn)
                {
                    UpdateRooms(i);
                }
            }
        }

        public void UpdateRooms(UInt32 ClientID)
        {
            Program.Client[ClientID].clientHandler.SendCommand(ClientID, "1\n");//Clear
            foreach (String Room in GetRooms(ClientID))
            {
                Program.Client[ClientID].clientHandler.SendCommand(ClientID, "1\n" + Room);
            }
        }

        public void LeaveRoom(UInt32 ClientID)
        {
            UInt32 RoomID = Program.Client[ClientID].Room;
            for (int i = 0; i < Rooms[RoomID].Members.Length; i++)//Remove player
            {
                if(Rooms[RoomID].Members[i] == ClientID)
                {
                    Rooms[RoomID].Members[i] = 0;
                    Rooms[RoomID].Size--;
                    Program.Client[ClientID].Room = 0;
                    break;
                }
            }

            if (Rooms[RoomID].Size != 0)//Promote players
            {
                for (int i = 1; i < Rooms[RoomID].Members.Length; i++)
                {
                    if (Rooms[RoomID].Members[i] != 0 && Rooms[RoomID].Members[i - 1] == 0)
                    {
                        Rooms[RoomID].Members[i - 1] = Rooms[RoomID].Members[i];
                        Rooms[RoomID].Members[i] = 0;
                        i = 1;
                    }
                }
            }

            Boolean RoomEmpty = true;
            foreach(UInt32 Member in Rooms[RoomID].Members)
            {
                if(Member != 0)
                {
                    RoomEmpty = false;
                    Program.Client[Member].clientHandler.SendCommand(Member, "3\n");
                }
            }

            if (RoomEmpty)
            {
                if (Program.TotalRooms > 0)
                {
                    Program.TotalRooms--;
                    Program.Client[ClientID].clientHandler.SendStatsAll();
                }
                Program.Log("has closed room " + RoomID.ToString() + ".", 0);
            }
            UpdateRoomsAll(ClientID);
        }

        private String[] GetRooms(UInt32 ClientID)
        {
            List<String> result = new List<String>();
            for (int i = 1; i < Rooms.Length; i++)//Open rooms.
            {
                if (i == Program.Client[ClientID].Room)//Skip your room
                {
                    continue;
                }

                String RoomStr;
                if ((Rooms[i].Size != 0) && (Rooms[i].Size < Rooms[i].Members.Length))
                {
                    RoomStr = i.ToString() + "\n" + Rooms[i].Name;
                    foreach (UInt32 Member in Rooms[i].Members)
                    {
                        RoomStr += "\n" + Program.Client[Member].InGameName;
                    }
                    result.Add(RoomStr);
                }
            }

            for (int i = 1; i < Rooms.Length; i++)//Full rooms.
            {
                if (i == Program.Client[ClientID].Room)//Skip your room
                {
                    continue;
                }

                String RoomStr;
                if (Rooms[i].Size == 4)
                {
                    RoomStr = i.ToString() + "\n" + Rooms[i].Name;
                    foreach (UInt32 Member in Rooms[i].Members)
                    {
                        RoomStr += "\n" + Program.Client[Member].InGameName;
                    }
                    result.Add(RoomStr);
                }
            }
            return result.ToArray();
        }

        public void SendMessage(String msg, UInt32 ClientID)
        {
            UInt32 Room = Program.Client[ClientID].Room;
            for (uint i = 0; i < Rooms[Room].Members.Length; i++)
            {
                if ((Rooms[Room].Members[i] != ClientID) && (Rooms[Room].Members[i] != 0))
                {
                    Byte[] Response = Encoding.UTF8.GetBytes("XX" + "9\n<b>" + Program.Client[ClientID].InGameName + "</b>:&nbsp;" + msg + "<br>");
                    if ((Response.Length - 2) >= 0x7D)
                    {
                        Response = Encoding.UTF8.GetBytes("XXXX" + "9\n<b>" + Program.Client[ClientID].InGameName + "</b>:&nbsp;" + msg + "<br>");
                        Response[0] = 0x81;
                        Response[1] = 0x7E;
                        Response[2] = (byte)((Response.Length - 4) >> 0x08);
                        Response[3] = (byte)((Response.Length - 4) & 0xFF);
                    }
                    else
                    {
                        Response[0] = 0x81;
                        Response[1] = (byte)(Response.Length - 2);
                    }
                    Program.Client[ClientID].clientHandler.SendMessage(Rooms[Room].Members[i], Response);
                }
            }
        }
    }
}