using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using pb = Google.ProtocolBuffers;

namespace shareDesktopClient
{
    public delegate void OnGetScreen(chat.Message msg);
    public class SocketController
    {



        private User _user = null;

        private Session session = null;

        public OnGetScreen OnGetScreen
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="c"></param>
        public SocketController(Control c)
        {
            if (session == null)
            {
                session = new Session();
                session.Dispatcher = c;
                
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        public void SetUser(string username)
        {
            if (!string.IsNullOrEmpty(username))
            {
                _user = new User{
                    Name = username,
                    Session = this.session
                };
                if(OnGetScreen != null)
                {
                    _user.OnGetScreen = OnGetScreen;

                }
                
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public void LoginTo(string ip, string port)
        {
            #region
            if (_user == null)
                throw new Exception("please setuser be first;");

            session.Address = ip;
            session.Port = Int32.Parse(port);
            session.MessageHandler = OnMessage;
            session.OnClose = OnClose;
            session.OnConnection = OnConnection;

            session.StartConnection();
            #endregion
        }

        


        void OnMessage(chat.Message msg)
        {
            #region
            if (msg.HasNotification)
            {
                if (msg.Notification.HasWelcome)
                {
                    //this.Title = msg.Notification.Welcome.Text.ToStringUtf8();
                }
                else if (msg.Notification.HasFriend)
                {
                    var name = msg.Notification.Friend.Name.ToStringUtf8();
                    if (msg.Notification.Friend.Online)
                    {
                        //my_friends.Add(name);
                    }
                    else
                    {
                        //my_friends.Remove(name);
                    }
                }
                else if (msg.Notification.HasScreen)
                {
                    if (OnGetScreen != null)
                        OnGetScreen(msg);
                }
                else if (msg.Notification.HasMsg)
                {
                    //var txtMsg = new TextMessage(msg.Notification.Msg);
                    //text_messages.Add(txtMsg);

                }
            }
            #endregion
        }

        void OnConnection(IAsyncResult ar)
        {
            this._user.Login();
            Console.WriteLine("relogin in {0}.", DateTime.Now);
            Thread.Sleep(3000);
        }

        
        void OnClose(IAsyncResult ar)
        {
            session = null;
        }

    }
}
