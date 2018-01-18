using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace shareDesktopClient
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(
            object sender, EventArgs e)
        {
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {

            SocketController socketcontrol = new SocketController(this);

            socketcontrol.OnGetScreen += new OnGetScreen((msg) =>
            {
                try
                {
                    MemoryStream ms = new MemoryStream(msg.Notification.Screen.FileBytes.ToByteArray());

                    this.newPanel1.BackgroundImage = Bitmap.FromStream(ms, true);

                }
                catch(Exception e1)
                {
                    Console.WriteLine(e1.Message);
                }
            });

            socketcontrol.SetUser(Guid.NewGuid().ToString());

            socketcontrol.LoginTo(this.tbIP.Text.Trim(), this.tbPort.Text.Trim());


            this.btnConnect.Enabled = false;
            this.tbIP.Enabled = false;
            this.tbPort.Enabled = false;
        }


    }
}
