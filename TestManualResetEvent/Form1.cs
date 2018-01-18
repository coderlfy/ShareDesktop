using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TestManualResetEvent
{
    public partial class Form1 : Form
    {
        private static ManualResetEvent receiveDone = new ManualResetEvent(false);
        public Form1()
        {
            InitializeComponent();

        }

        private void Form1_Load(
            object sender, EventArgs e)
        {
            Thread t = new Thread(new ThreadStart(() => {
                int i=0;
                while(true)
                {
                    receiveDone.WaitOne();
                    Console.WriteLine(++i);
                    receiveDone.Reset();
                    Thread.Sleep(1000);
                }
                
            }));
            t.IsBackground = true;
            t.Start();

        }

        private void button1_Click(object sender, EventArgs e)
        {
            receiveDone.Set();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            receiveDone.WaitOne();
        }
    }
}
