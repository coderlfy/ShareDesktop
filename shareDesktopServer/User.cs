using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using chat;
using pb = Google.ProtocolBuffers;
using System.Threading;


namespace shareDesktopServer
{
    public class User
    {
        private List<byte[]> wait_for_send_ = new List<byte[]>();
        private bool can_send_data_ = true;
        private uint sequence_ = 0;
        private uint session_id_ = 0;

        private byte[] head = new byte[4];
        private byte[] body = null;
        private int body_read_len = 0;

        private string username_ = null;
        private bool login = false;

        static uint next_session_id_ = 1;
        public User(TcpClient tcp_conn)
        {
            connection_ = tcp_conn;
            session_id_ = next_session_id_;
            next_session_id_++;

            Users.instance().AddUser(this);

            StartReadHead();


        }
        public void Welcome()
        {
            Message welcome = new Message.Builder()
           {
               MsgType = MSG.Welcome_Notification,
               Sequence = 0xffffffff,
               SessionId = this.SessionId,
               Notification = new Notification.Builder()
               {
                   Welcome = new WelcomeNotification.Builder()
                   {
                       Text = pb.ByteString.CopyFromUtf8("欢迎加入广播广播聊天服务。")
                   }.Build()
               }.Build()
           }.Build();

            SendMessage(welcome);
        }
        public string Username
        {
            get { return username_; }
            set { username_ = value; }
        }
        public bool Login
        {
            get { return login; }
            set { login = value; }
        }
        public uint Sequence
        {
            get
            {
                sequence_++;
                return sequence_;
            }
        }
        private DateTime _sendScreen = DateTime.Now;

        public DateTime SendScreen
        {
            get { return _sendScreen; }
            set { _sendScreen = value; }
        }
        
        public uint SessionId
        {
            get { return session_id_; }
            set { session_id_ = value; }
        }
        TcpClient connection_ = null;
        TcpClient Connection
        {
            get { return connection_; }
            set { connection_ = value; }
        }

        void InternalSendMessage(byte[] msg_data)
        {
            if (connection_ != null && connection_.Connected)
            {
                try
                {
                    connection_.Client.BeginSend(msg_data, 0, msg_data.Length, 0, new AsyncCallback(OnSendComplete), msg_data);
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                    InternalSendMessage(msg_data);
                }
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
        public void SendMessage(byte[] msg_data)
        {
            lock (wait_for_send_)
            {
                if (!can_send_data_)
                {
                    //wait_for_send_.Add(msg_data);
                }
                else
                {
                    can_send_data_ = false;
                    InternalSendMessage(msg_data);
                }
            }
        }
        void StartReadHead()
        {
            connection_.Client.BeginReceive(head, 0, head.Length, 0, new AsyncCallback(OnReadHead), null);
        }
        void OnReadHead(IAsyncResult ar)
        {
            // byte[] head = ar.AsyncState as byte[];
            try
            {
                int len = connection_.Client.EndReceive(ar);
                if (len == 4)
                {
                    int body_len = (head[0] & 0x000000ff) << 24
                    | (head[1] & 0x000000ff) << 16
                    | (head[2] & 0x000000ff) << 8
                    | (head[3] & 0x000000ff);
                    body = new byte[body_len];
                    body_read_len = 0;
                    connection_.Client.BeginReceive(body, 0, body.Length, 0, new AsyncCallback(OnReadBody), null);
                }
                else
                {
                    ///
                    Console.WriteLine("read head ..error");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("OnReadHead Exception:{0}", e.Message);
                Users.instance().DelUser(this);
                //var friendNotification = BuildFriendNotification(false);
                //Users.instance().forall((User otherUser) =>
                //{
                //    if (otherUser.login)
                //    {
                //        otherUser.SendMessage(friendNotification);
                //    }
                //});
            }
        }
        void OnReadBody(IAsyncResult ar)
        {
            try
            {
                int len = connection_.Client.EndReceive(ar);

                body_read_len += len;
                if (body_read_len < body.Length)
                {
                    connection_.Client.BeginReceive(
                        body, body_read_len, body.Length - body_read_len, 0,
                        new AsyncCallback(OnReadBody), null);
                    return;
                }
                StartReadHead();
                try
                {
                    chat.Message recv_msg = chat.Message.ParseFrom(body);

                    switch (recv_msg.MsgType)
                    {
                        case chat.MSG.Login_Request:
                            {
                                Username = recv_msg.Request.Login.Username.ToStringUtf8();

                                chat.Message login_rsp = new chat.Message.Builder()
                                {
                                    MsgType = MSG.Login_Response,
                                    Sequence = recv_msg.Sequence,
                                    SessionId = this.SessionId,
                                    Response = new Response.Builder()
                                    {
                                        Result = true,
                                        LastResponse = true,
                                        Login = new LoginResponse.Builder()
                                        {
                                            Ttl = 10
                                        }.Build()
                                    }.Build()
                                }.Build();
                                Login = true;

                                SendMessage(login_rsp);

                                var friendNotification = BuildFriendNotification(true);
                                Users.instance().forall((User otherUser) =>
                                {
                                    if (otherUser != this && otherUser.login)
                                    {
                                        otherUser.SendMessage(friendNotification);
                                    }
                                });


                            }
                            break;

                        //case chat.MSG.GetScreen_Request:
                        //    {
                        //        lock (BMPScreen.IsLockGetJpgBytes)
                        //        {
                        //            if (this.SendScreen.AddSeconds(1) < DateTime.Now)
                        //            {
                        //                chat.Message screenresponse = new chat.Message.Builder()
                        //                {
                        //                    MsgType = chat.MSG.GetScreen_Response,
                        //                    Sequence = recv_msg.Sequence,
                        //                    Response = new Response.Builder()
                        //                    {
                        //                        Result = true,
                        //                        LastResponse = true,
                        //                        Screenfile = new GetScreenResponse.Builder()
                        //                        {
                        //                            FileBytes = pb.ByteString.CopyFrom(BMPScreen.JpgBytes)
                        //                        }.Build()
                        //                    }.Build()
                        //                }.Build();
                        //                SendMessage(screenresponse);

                        //                this.SendScreen = DateTime.Now;
                        //            }
                        //            //Console.WriteLine(this.Username);
                        //        }

                        //    }
                        //    break;
                        case chat.MSG.Logout_Request:
                            {
                                Message rsp_msg = new Message.Builder()
                                {
                                    MsgType = MSG.Logout_Response,
                                    Sequence = recv_msg.Sequence,
                                    SessionId = this.SessionId,
                                    Response = new Response.Builder()
                                    {
                                        Result = true,
                                        LastResponse = true
                                    }.Build()
                                }.Build();

                                SendMessage(rsp_msg);

                                Login = false;
                                var friendNotification = BuildFriendNotification(false);
                                Users.instance().forall((User otherUser) =>
                                {
                                    if (otherUser != this && otherUser.login)
                                    {
                                        otherUser.SendMessage(friendNotification);
                                    }
                                });
                            }
                            break;
                        case chat.MSG.Keepalive_Request:
                            {
                                Message rsp_msg = new Message.Builder()
                                {
                                    MsgType = MSG.Keepalive_Response,
                                    Sequence = recv_msg.Sequence,
                                    SessionId = this.SessionId,
                                    Response = new Response.Builder()
                                    {
                                        Result = true,
                                        LastResponse = true
                                    }.Build()
                                }.Build();

                                SendMessage(rsp_msg);
                            }
                            break;
                        case chat.MSG.Get_Friends_Request:
                            {
                                GetFriendsResponse.Builder friends = new GetFriendsResponse.Builder();
                                Users.instance().forall((User usr) =>
                                {
                                    Friend friend = new Friend.Builder()
                                    {
                                        Name = pb.ByteString.CopyFromUtf8(usr.Username),
                                        Online = usr.Login
                                    }.Build();

                                    friends.AddFriends(friend);
                                });

                                Message rsp_msg = new Message.Builder()
                                {
                                    MsgType = MSG.Get_Friends_Response,
                                    Sequence = recv_msg.Sequence,
                                    SessionId = this.SessionId,
                                    Response = new Response.Builder()
                                    {
                                        Result = true,
                                        LastResponse = true,
                                        GetFriends = friends.Build()
                                    }.Build()
                                }.Build();

                                SendMessage(rsp_msg);
                            }
                            break;
                        case chat.MSG.Send_Message_Request:
                            {
                                Message rsp_msg = new Message.Builder()
                                {
                                    MsgType = MSG.Send_Message_Response,
                                    Sequence = recv_msg.Sequence,
                                    SessionId = this.SessionId,
                                    Response = new Response.Builder()
                                    {
                                        Result = true,
                                        LastResponse = true
                                    }.Build()
                                }.Build();

                                SendMessage(rsp_msg);

                                Message text_msg = new Message.Builder()
                                {
                                    MsgType = MSG.Message_Notification,
                                    Sequence = 0xffffffff,
                                    Notification = new Notification.Builder()
                                    {
                                        Msg = new MessageNotification.Builder()
                                        {
                                            Sender = pb.ByteString.CopyFromUtf8(Username),
                                            Text = recv_msg.Request.SendMessage.Text,
                                            Timestamp = DateTime.Now.ToString()
                                        }.Build()
                                    }.Build()
                                }.Build();

                                if (recv_msg.Request.SendMessage.HasReceiver)
                                {
                                    string receiver = recv_msg.Request.SendMessage.Receiver.ToStringUtf8();

                                    Users.instance().forall((User usr) =>
                                    {
                                        if (usr.Username.Equals(receiver))
                                        {
                                            usr.SendMessage(text_msg);
                                        }
                                    });
                                }
                                else
                                {
                                    Users.instance().forall((User usr) =>
                                    {
                                        usr.SendMessage(text_msg);

                                    });
                                }
                            }
                            break;
                        default:
                            break;

                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("ParserMessage Exception:{0}", e.Message);

                }
            }
            catch (SocketException sockectex)
            {
                Console.WriteLine("OnReadBody SockectException:{0}", sockectex.Message);
                Users.instance().DelUser(this);

            }
            catch (Exception ex)
            {
                Console.WriteLine("OnReadBody Exception:{0}", ex.Message);
            }

        }

        private Message BuildFriendNotification(bool online)
        {
            chat.Message friend_notification_msg = new Message.Builder()
            {
                MsgType = MSG.Friend_Notification,
                Sequence = 0xffffffff,
                Notification = new Notification.Builder()
                {
                    Friend = new FriendNotification.Builder()
                    {
                        Name = pb.ByteString.CopyFromUtf8(Username),
                        Online = online
                    }.Build()
                }.Build()
            }.Build();
            return friend_notification_msg;
        }
        void OnSendComplete(IAsyncResult ar)
        {
            try
            {
                var len = connection_.Client.EndSend(ar);
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
                    lock (wait_for_send_)
                    {
                        if (wait_for_send_.Count > 0)
                        {
                            InternalSendMessage(wait_for_send_[0]);
                            Console.WriteLine("-----------wait_for_send_.count={0}------------", wait_for_send_.Count);
                            wait_for_send_.RemoveAt(0);
                        }
                        else
                        {
                            can_send_data_ = true;
                        }
                    }
                }
                Thread.Sleep(500);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
    }

    public delegate void EachUser(User user);
    public delegate bool AnyUser(User user);
    public class Users
    {
        private List<User> users_ = new List<User>();

        public void AddUser(User user)
        {
            lock (users_)
            {
                users_.Add(user);
            }

        }
        public void DelUser(User user)
        {
            lock (users_)
            {
                users_.Remove(user);
            }
        }

        public void forall(EachUser callback)
        {
            lock (users_)
            {
                if (users_.Count == 0)
                {
                    return;
                }
                foreach (var usr in users_)
                {
                    callback(usr);
                }
            }

        }
        public bool forany(AnyUser callback)
        {
            lock (users_)
            {
                if (users_.Count == 0)
                {
                    return false;
                }
                foreach (var usr in users_)
                {
                    if (callback(usr))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static Users instance()
        {
            return g_users_;
        }
        private static Users g_users_ = new Users();
    }



}
