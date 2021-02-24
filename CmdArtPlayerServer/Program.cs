using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CmdArtPlayerServer
{
    class Program
    {
        static void Main(string[] args)
        {
            int maxUsers = 2;
            int port = 25568;

            if (args.Length < 2)
            {
                Console.WriteLine("Please input parameter by .bat file. ex: run.exe -10 -25568");
                Console.Read();
                return;
            }

            if (!int.TryParse(args[0].Substring(1), out maxUsers))
            {
                Console.WriteLine("Error: 1st args 'MaxUses' is not a number.");
                Console.Read();
                return;
            }

            if (!int.TryParse(args[1].Substring(1), out port))
            {
                Console.WriteLine("Error: 2nd args 'Port' is not a number.");
                Console.Read();
                return;
            }

            Debug.Log($"MaxUses: {maxUsers} , Port: {port} . Prepare to Start Server ...");

            Program main = new Program();
            main.maxUsers = maxUsers;
            main.serverPort = port;
            main.Start();
        }

        public int serverPort = 25568;
        public int maxUsers = 10;
        public int recvBufferSize = 1024;
        public string EndToken = "[/TCP]";

        public Action<string> OnSignalReceived;
        public Action OnUserConnected;

        // Private works
        Socket serverSocket; //服務器端socket  
        Socket[] clientSockets; //客戶端socket  
        IPEndPoint ipEnd; //偵聽端口  
        string[] token;
        Thread[] connectThread; //連接線程
        Action ActionQueue;

        // Use this for initialization
        void Start()
        {
            token = new string[] { EndToken };
            InitSocket();

            OnSignalReceived += RecieveMessages;
            OnUserConnected += PrepareToStart;

            while (true)
            {
                string input = Console.ReadLine();
                SocketSend(input);
            }
        }

        void Update()
        {
            if (ActionQueue != null)
            {
                ActionQueue?.Invoke();
                ActionQueue = null;
            }
        }

        public void InitSocket()
        {
            //定義偵聽端口,偵聽任何IP  
            ipEnd = new IPEndPoint(IPAddress.Any, serverPort);
            //定義套接字類型,在主線程中定義
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(ipEnd);
            //開始偵聽,最大10個連接  
            serverSocket.Listen(maxUsers);

            ClearThreads();
            ClearClients();
            for (int i = 0; i < maxUsers; i++)
            {
                //開啟一個線程連接，必須的，否則主線程卡死
                int targetIndex = i;
                connectThread[targetIndex] = new Thread(() => ServerWork(targetIndex));
                connectThread[targetIndex].Start();
            }

            Debug.Log($"Start Server at :{serverPort} , with {maxUsers} thread.");
        }

        void ServerWork(int index)
        {
            //連接
            Socket currSocket = SocketConnet(index);
            //進入接收循環  
            while (true)
            {
                byte[] recvData = new byte[recvBufferSize];
                int recvLen = 0;
                string recvStr;

                try
                {
                    //獲取收到的數據的長度  
                    recvLen = currSocket.Receive(recvData);
                }
                catch (System.Net.Sockets.SocketException)
                {
                    SocketConnet(index);
                    continue;
                }
                //如果收到的數據長度為0，則重連並進入下一個循環  
                if (recvLen == 0)
                {
                    SocketConnet(index);
                    continue;
                }
                //輸出接收到的數據  
                recvStr = Encoding.UTF8.GetString(recvData, 0, recvLen);

                //N[/TCP]
                //IPEndPoint ipEndClient = (IPEndPoint)currSocket.RemoteEndPoint;
                //Debug.Log($"cur port: {ipEndClient.Port}");
                //string cont = "";
                //foreach (var item in recvData)
                //{
                //    cont += item + " ";
                //}
                //Debug.Log(cont);
                //Debug.Log(recvStr);

                //Recieve Data Will Be   245,135,90[/TCP]   , str 不會包含[/TCP]
                string[] clearString = recvStr.Split(token, StringSplitOptions.None);  // => N , [/TCP]

                if (clearString.Length > 1)
                {
                    Debug.Log($"TCP >> Recieved : {clearString[0]}");

                    OnSignalReceived.Invoke(clearString[0]);
                } // end Length

            }  // end While
        }

        Socket SocketConnet(int index)
        {
            if (clientSockets[index] != null)
                clientSockets[index].Close();

            //控制台輸出偵聽狀態
            Debug.Log($"Waiting for a client ({index})");
            //一旦接受連接，創建一個客戶端  
            clientSockets[index] = serverSocket.Accept();
            //獲取客戶端的IP和端口  
            IPEndPoint ipEndClient = (IPEndPoint)clientSockets[index].RemoteEndPoint;
            //輸出客戶端的IP和端口  
            Debug.Log($"Thread ({index}) Connect with " + ipEndClient.Address.ToString() + ":" + ipEndClient.Port.ToString());

            //連接成功則發送數據  
            //sendStr="Welcome to my server";
            //SocketSend(sendStr);  

            OnUserConnected?.Invoke();

            return clientSockets[index];
        }

        //Data to Glass can use UTF8
        public void SocketSend(string sendStr)
        {
            int i = 0;
            foreach (var clientSocket in clientSockets)
            {
                if (clientSocket == null)
                    continue;
                if (clientSocket.Connected == false)
                    continue;
                try
                {
                    string toSend = sendStr + EndToken;
                    //清空發送緩存  
                    var sendData = new byte[1024];
                    //數據類型轉換  
                    sendData = Encoding.UTF8.GetBytes(toSend);
                    //發送  
                    clientSocket.Send(sendData, sendData.Length, SocketFlags.None);

                    Debug.Log($"TCP >> Send: {toSend}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e.Message.ToString());
                }
                i++;
            }
            Debug.Log($"total message {i} send.");
        }

        void ClearThreads()
        {
            if (connectThread != null)
            {
                foreach (var item in connectThread)
                {
                    if (item != null)
                    {
                        item.Interrupt();
                        item.Abort();
                    }

                }
            }
            connectThread = new Thread[maxUsers];
        }

        void ClearClients()
        {
            if (clientSockets != null)
            {
                foreach (var item in clientSockets)
                {
                    if (item != null)
                    {
                        item.Close();
                    }
                }
            }
            clientSockets = new Socket[maxUsers];
        }

        void CloseSocket()
        {
            //先關閉客戶端  
            ClearClients();

            //再關閉線程  
            ClearThreads();
            //最後關閉服務器
            if (serverSocket != null)
            {
                serverSocket.Close();
                Debug.Log("diconnect.");
            }
        }

        void OnApplicationQuit()
        {
            CloseSocket();
        }

        int totalEnd = 0;
        void RecieveMessages(string str)
        {
            if(str == "End")
            {
                totalEnd += 1;
            }

            if(totalEnd >= maxUsers)
            {
                Debug.Log("All Video Finished. Wait for replay...");


                totalEnd = 0;
                DelayReplay();
            }
        }

        async void DelayReplay()
        {
            await Task.Delay(3000);
            SocketSend("Play");
        }

        void PrepareToStart()
        {
            int currUsers = 0;

            foreach (var clientSocket in clientSockets)
            {
                if (clientSocket == null)
                    continue;

                if (clientSocket.Connected)
                {
                    currUsers += 1;
                }
            }

            if(currUsers >= maxUsers)
            {
                SocketSend("Play");
            }
        }
    }
}
