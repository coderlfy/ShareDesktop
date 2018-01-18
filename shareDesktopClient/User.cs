using Google.ProtocolBuffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace shareDesktopClient
{
    public class User
    {
        private string _name;

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }


        private Session _session;

        public Session Session
        {
            get { return _session; }
            set { _session = value; }
        }

        public OnGetScreen OnGetScreen
        {
            get;
            set;
        }

        public void Login()
        {
            #region

            chat.Message login = new chat.Message.Builder()
            {
                MsgType = chat.MSG.Login_Request,
                Sequence = Session.Sequence,
                Request = new chat.Request.Builder()
                {
                    Login = new chat.LoginRequest.Builder()
                    {
                        Username = ByteString.CopyFromUtf8(this.Name)
                    }.Build()
                }.Build()
            }.Build();

            Transaction<chat.Message>.AddRequest(login.Sequence, (chat.Message rsp_msg) =>
            {
                if (rsp_msg.HasResponse &&
                    rsp_msg.Response.HasResult &&
                    rsp_msg.Response.Result)
                {
                    Session.SessionId = rsp_msg.SessionId;
                    Console.WriteLine("login success!");
                    //this.login.IsEnabled = false;
                    //this.logout.IsEnabled = true;
                    //GetFriends();

                    //Thread t = new Thread(new ThreadStart(() =>
                    //{
                    //    this.StartScreenRequest();
                    //}));
                    //t.IsBackground = true;
                    //t.Start();

                }
            });
            Session.SendMessage(login);
            #endregion
        }

        public void StartScreenRequest()
        {
            #region
            while (true)
            {
                try
                {
                    chat.Message getscreen = new chat.Message.Builder()
                    {
                        MsgType = chat.MSG.GetScreen_Request,
                        Sequence = Session.Sequence,
                        Request = new chat.Request.Builder()
                        {
                            Getscreen = new chat.GetScreenRequest.Builder()
                            {
                                Username = this.Name
                            }.Build()
                        }.Build()
                    }.Build();

                    Transaction<chat.Message>.AddRequest(
                        getscreen.Sequence,
                        (chat.Message rsp_msg) =>
                        {
                            if (rsp_msg.HasResponse &&
                                rsp_msg.Response.HasResult &&
                                rsp_msg.Response.Result)
                            {
                                if (OnGetScreen != null)
                                    OnGetScreen(rsp_msg);
                            }
                        });

                    Session.SendMessage(getscreen);

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);

                }

                Thread.Sleep(1000);
            }
            #endregion
        }
    }
}
