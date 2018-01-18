using chat;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using pb = Google.ProtocolBuffers;

namespace shareDesktopServer
{
    public class BMPScreen
    {
        /// <summary>
        /// 访问（读写操作）时需加IsLockGetJpgBytes控制
        /// </summary>
        public static byte[] JpgBytes = null;

        /// <summary>
        /// 读写JpgBytes的锁开关量
        /// </summary>
        public static object IsLockGetJpgBytes = true;

        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(out CURSORINFO pci);

        private const Int32 CURSOR_SHOWING = 0x00000001;
        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public Int32 x;
            public Int32 y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO
        {
            public Int32 cbSize;
            public Int32 flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        /// <summary>
        /// 获取屏幕的bmp（带光标）
        /// </summary>
        /// <returns></returns>
        private static Bitmap getScreen()
        {
            #region
            int width = Screen.PrimaryScreen.Bounds.Width;
            int height = Screen.PrimaryScreen.Bounds.Height;
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(0, 0, 0, 0, new Size(width, height));
                CURSORINFO pci;
                pci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
                while (true)
                {
                    bool issuccess = GetCursorInfo(out pci);
                    if (issuccess && pci.hCursor != IntPtr.Zero)
                        break;

                    Thread.Sleep(100);
                }
                System.Windows.Forms.Cursor cur = new System.Windows.Forms.Cursor(pci.hCursor);
                cur.Draw(g, new Rectangle(pci.ptScreenPos.x - 10, pci.ptScreenPos.y - 10, cur.Size.Width, cur.Size.Height));
            }

            //bmp.compress()
            return bmp;
            #endregion
        }

        private static EncoderParameters m_ps = null;

        private static EncoderParameters _ps
        {
            #region
            get
            {
                if(m_ps == null)
                {
                    EncoderParameter p;

                    m_ps = new EncoderParameters(1);

                    p = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 15L);
                    m_ps.Param[0] = p;
                }
                return m_ps;
            }
            #endregion
        }

        /// <summary>
        /// 将bmp转为bytes
        /// </summary>
        /// <param name="bmp"></param>
        /// <returns></returns>
        private static byte[] getJpgByte(Bitmap bmp)
        {
            #region
            MemoryStream ms = new MemoryStream();

            bmp.Save(ms, GetCodecInfo("image/jpeg"), _ps);

            byte[] buffer = new byte[ms.Length];
            //Image.Save()会改变MemoryStream的Position，需要重新Seek到Begin
            ms.Seek(0, SeekOrigin.Begin);
            ms.Read(buffer, 0, buffer.Length);
            return buffer;
            #endregion
        }

        /// <summary>
        /// 保存JPG时用
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns>得到指定mimeType的ImageCodecInfo</returns>
        private static ImageCodecInfo GetCodecInfo(string mimeType)
        {
            #region
            ImageCodecInfo[] CodecInfo = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo ici in CodecInfo)
            {
                if (ici.MimeType == mimeType) return ici;
            }
            return null;
            #endregion
        }

        /// <summary>
        /// 不停获取屏幕的图像bytes
        /// </summary>
        public static void startGetScreenJpgBytes()
        {
            #region
            while (true)
            {
                lock (IsLockGetJpgBytes)
                {
                    try
                    {
                        JpgBytes = BMPScreen.getJpgByte(BMPScreen.getScreen());
                        chat.Message screennotify = new chat.Message.Builder()
                        {
                            MsgType = chat.MSG.Screen_Notification,
                            Sequence = 0xffffffff,
                            Notification = new Notification.Builder()
                            {
                                Screen = new ScreenNotification.Builder()
                                {
                                    FileBytes = pb.ByteString.CopyFrom(BMPScreen.JpgBytes)
                                }.Build()
                            }.Build()
                        }.Build();

                        Users.instance().forall((User otherUser) =>
                        {
                            otherUser.SendMessage(screennotify);
                        });

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }

                Thread.Sleep(1000);

            }
            #endregion
        }

    }
}
