﻿using System;
using System.Net.Sockets;
using System.Windows.Forms;

namespace Client
{
    public class RemoteClient
    {
        //端口回传
        public delegate void GetPortEventHandler(string ip, int port);
        public event GetPortEventHandler GetPortEvent;

        private const int BufferSize = 1024;
        private byte[] buffer;
        private TcpClient client;
        private NetworkStream streamToServer;
        UdpForwardClient udpForwardClient = null;
        private string serverIP = "";
        private int serverPort = 20000;

        public RemoteClient(string serverIP)
        {
            client = new TcpClient();
            client.Connect(serverIP, serverPort);
            this.serverIP = serverIP;
            buffer = new byte[BufferSize];

            streamToServer = client.GetStream();
        }

        private void SendMessage(byte[] messageSend)
        {

            streamToServer.Write(messageSend, 0, messageSend.Length);

            lock (streamToServer)
            {
                AsyncCallback callBack = new AsyncCallback(ReadComplete);
                streamToServer.BeginRead(buffer, 0, BufferSize, callBack, null);
            }
        }

        public void GetNewPort()
        {
            byte[] messageSend = Model.Encode(Model.Client_Arrive_Handshake);
            SendMessage(messageSend);
            //获取新端口
        }

        //读取完成时的回调方法
        private void ReadComplete(IAsyncResult ar)
        {
            int bytesRead;

            try
            {
                lock (streamToServer)
                {
                    bytesRead = streamToServer.EndRead(ar);
                }

                //TODO 服务端断开事件
                if (bytesRead == 0)
                {
                    throw new Exception("已断开连接");
                }

                string[] messageArrive = Model.Decode(bytesRead, buffer);
                Array.Clear(buffer, 0, buffer.Length);// 清空缓存，避免脏读

                if (messageArrive[0] == Model.Server_Proxy_Start)
                {
                    //传回分配的端口
                    int remotePort = Convert.ToInt32(messageArrive[1]);

                    //回传端口号
                    if (GetPortEvent!=null)
                    {
                        GetPortEvent(serverIP, remotePort);
                    }

                    //销毁转发（如果存在）
                    if (udpForwardClient != null)
                    {
                        removeForward();
                    }
                    //开始转发
                    udpForwardClient = new UdpForwardClient(serverIP, remotePort);

                    //TODO 其他处理
                }

                lock (streamToServer)
                {
                    AsyncCallback callBack = new AsyncCallback(ReadComplete);
                    streamToServer.BeginRead(buffer, 0, BufferSize, callBack, null);
                }
            }
            catch (Exception ex)
            {
                if (streamToServer != null)
                    streamToServer.Dispose();
                client.Close();

                MessageBox.Show(ex.Message.ToString(), "错误");
                removeForward();
            }
        }
        public void removeForward()//销毁转发
        {
            if (udpForwardClient!=null)
            {
                udpForwardClient.Close();
                udpForwardClient = null;
            }
        }
    }
}
