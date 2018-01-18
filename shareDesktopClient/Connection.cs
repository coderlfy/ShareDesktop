using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using pb = Google.ProtocolBuffers;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using System.Threading;
//using System.Windows.Threading;

namespace shareDesktopClient
{
    public delegate void OnConnection(IAsyncResult ar);
    public delegate void OnClose(IAsyncResult ar);
    public delegate void OnMessage( chat.Message msg);

    public class Session
    {
        private TcpClient client = null;
        private byte[] head = new byte[4];
        private byte[] body = null;
        private List< byte []> wait_for_send = new List<byte[]>();
        private bool sending = false;
        private int body_read_len = 0;
        private uint sequence = 0;
        private ObservableCollection<string> errors = new ObservableCollection<string>();
        
        public TcpClient Client
        {
            get { return client; }
        }

        public Control Dispatcher
        {
            get;
            set;
        }
        public OnConnection OnConnection
        {
            get;
            set;
        }
        public OnClose OnClose
        {
            get;
            set;
        }
     
      
        public String Address
        {
            get;
            set;
        }
        public int Port
        {
            get;
            set;
        }

        public uint Sequence
        {
            get
            {
                sequence++;
                return sequence;
            }
        }
        public uint SessionId
        {
            get;
            set;
        }
        public ObservableCollection<string> Errors
        {
            get { return errors; }
        }
        public OnMessage MessageHandler
        {
            get;
            set;
        }
       public void StartConnection() 
        {
            if (client == null)
            {
                client = new TcpClient();
            }
            if (!client.Connected)
            {
                client.BeginConnect(Address, Port, new AsyncCallback(OnSessionConnection), null);
            }
        }
        

        void OnSessionConnection(IAsyncResult ar)
        {
            //Dispatcher.BeginInvoke((Action)(() => { OnConnection(ar); }));
            OnConnection(ar);
            if (client.Connected)
            {
                StartReadHead();
            }
            else
            {
                try
                {
                    client.BeginConnect(Address, Port, new AsyncCallback(OnSessionConnection), null);
                }
                catch { }
            }
        }

        void StartReadHead()
        {
            client.Client.BeginReceive(head, 0, head.Length, 0, new AsyncCallback(OnReadHead), null);
        }
        void OnReadHead(IAsyncResult ar)
        {
           // byte[] head = ar.AsyncState as byte[];
            try
            {
                int len = client.Client.EndReceive(ar);
                if (len == 4)
                {
                    int body_len = (head[0] & 0x000000ff) << 24
                    | (head[1] & 0x000000ff) << 16
                    | (head[2] & 0x000000ff) << 8
                    | (head[3] & 0x000000ff);
                    body = new byte[body_len];
                    body_read_len = 0;
                    Console.WriteLine("try body len, {0}---------{1}", body_len, DateTime.Now);
                    client.Client.BeginReceive(body, 0, body.Length, 0, 
                        new AsyncCallback(OnReadBody), null);
                }
                else
                {
                    ///
                    Console.WriteLine("read head ..error");
                    throw new Exception("read head ..error");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("read head ..exception:{0}",ex.Message);
                client.Close();
                client = null;
                sending = false;
                this.StartConnection();

            }
        }
       
        void OnReadBody(IAsyncResult ar)
        {
            try
            {
                int len = client.Client.EndReceive(ar);

                body_read_len += len;
                if (body_read_len < body.Length)
                {
                    client.Client.BeginReceive(
                        body, body_read_len, body.Length - body_read_len, 0, 
                        new AsyncCallback(OnReadBody), null);
                    return;
                }
                StartReadHead();
                if (MessageHandler != null)
                {

                    chat.Message msg = chat.Message.ParseFrom(body);


                    if (msg.HasResponse)
                    {
                        Console.WriteLine(msg.MsgType);
                        Dispatcher.BeginInvoke((Action)(() =>
                        {
                            if (!msg.Response.Result)
                            {
                                errors.Add(msg.Response.ErrorDescribe.ToStringUtf8());
                            }
                            try
                            {
                                Transaction<chat.Message>.OnResponse(msg.Sequence, msg, msg.Response.LastResponse);
                            }
                            catch (Exception e)
                            {
                                errors.Add(e.Message);
                                errors.Add(e.StackTrace);
                            }
                        }));

                    }
                    else
                    {
                        Dispatcher.BeginInvoke((Action)(() => { MessageHandler(msg); }));
                    }

                }
            }
            catch( Exception ex)
            {
                Console.WriteLine(
                    "read body ..exception:{0}", 
                    ex.Message);

                client.Close();
                client = null;
                sending = false;
                this.StartConnection();

            }
        }
        void OnSendComplete(IAsyncResult ar)
        {
            var len = client.Client.EndSend(ar);
            var msg_data = ar.AsyncState as byte[];
            if (len < msg_data.Length)
            {
                var leave_data = new byte[msg_data.Length - len];
                for (int i = 0; i < leave_data.Length; i++)
                {
                    leave_data[i] = msg_data[len + i];
                }
                InternalSendMessage(leave_data);
                return;
            }
            else
            {
                lock (wait_for_send)
                {
                    if (wait_for_send.Count > 0)
                    {
                        InternalSendMessage(wait_for_send[0]);
                        wait_for_send.RemoveAt(0);
                    }
                    else
                    {
                        sending = false;
                    }
                }
            }

        }
        public void SendMessage(byte [] msg_data)
        {
            lock (wait_for_send)
            {
                if (sending)
                {
                    wait_for_send.Add(msg_data);
                }
                else
                {
                    InternalSendMessage(msg_data);
                }
            }
        }
        void InternalSendMessage(byte [] msg_data)
        {
            if (client != null && client.Connected)
            {
                sending = true;

                try
                {
                    client.Client.BeginSend(msg_data, 0, msg_data.Length, 0, new AsyncCallback(OnSendComplete), msg_data);

                }
                catch (SocketException ex)
                {
                    Console.WriteLine("Connection is interrupt!");
                    client.Close();
                    client = null;
                    sending = false;
                    this.StartConnection();
                }
                //client.Client.Send(msg_data);
            }
        }

        public void SendMessage(chat.Message msg)
        {
            int size = msg.SerializedSize;

            byte[] buf = new byte[size + 4];

            buf[0] = (byte)((size >> 24) & 0x000000ff);
            buf[1] = (byte)((size >> 16) & 0x000000ff);
            buf[2] = (byte)((size >> 8) & 0x000000ff);
            buf[3] = (byte)((size) & 0x000000ff);

            pb.CodedOutputStream cos = pb.CodedOutputStream.CreateInstance(buf, 4, size);
            msg.WriteTo(cos);

            SendMessage(buf);
        }
       
      
    }
}
