using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace shareDesktopClient
{
    public delegate void OnResponse<Message>(Message recvMessage);
    public class Transaction<Message>
    {

        private static object handlers_mutex_ = new object();
        private static Dictionary<uint, OnResponse<Message>> handlers = new Dictionary<uint, OnResponse<Message>>();


        public static void AddRequest(uint seq, OnResponse<Message> handler)
        {
            if (handler != null)
            {
                lock (handlers_mutex_)
                {
                    handlers.Add(seq, handler);
                }
            }
        }

        public static void OnResponse(uint seq, Message msg, bool last )
        {
            OnResponse<Message> msgHanlder = null;
            lock (handlers_mutex_)
            {
                if (handlers.ContainsKey(seq))
                {
                    msgHanlder = handlers[seq];
                    if (last)
                    {
                        handlers.Remove(seq);
                    }
                }
            }

            if (msgHanlder != null)
            {
                msgHanlder(msg);
            }
        }
    }

   
}
