using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WeiqiConnector
{
    class MouseKeyBoardController
    {
 //       备注：如果鼠标被移动，用设置MOUSEEVENTF_MOVE来表明，dX和dy保留移动的信息。给出的信息是绝对或相对整数值。
 //　　如果指定了MOWSEEVENTF_ABSOLOTE值，则dX和dy含有标准化的绝对坐标，其值在0到65535之间。事件程序将此坐标映射到显示表面。坐标（0，0）映射到显示表面的左上角，（6553，65535）映射到右下角。
 //　　如果没指定MOWSEEVENTF_ABSOLOTE，dX和dy表示相对于上次鼠标事件产生的位置（即上次报告的位置）的移动。正值表示鼠标向右（或下）移动；负值表示鼠标向左（或上）移动。
        [DllImport("user32")]
        public static extern int mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);
        //移动鼠标 
        public const int MOUSEEVENTF_MOVE = 0x0001;
        //模拟鼠标左键按下 
        public const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        //模拟鼠标左键抬起 
        public const int MOUSEEVENTF_LEFTUP = 0x0004;
        //模拟鼠标右键按下 
        public const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        //模拟鼠标右键抬起 
        public const int MOUSEEVENTF_RIGHTUP = 0x0010;
        //模拟鼠标中键按下 
        public const int MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        //模拟鼠标中键抬起 
        public const int MOUSEEVENTF_MIDDLEUP = 0x0040;
        //标示是否采用绝对坐标 
        public const int MOUSEEVENTF_ABSOLUTE = 0x8000;



        [DllImport("user32.dll", EntryPoint = "keybd_event", SetLastError = true)]
        private static extern void keybd_event(Keys bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
        const int KEYEVENTF_KEYUP = 0x02;
        const int KEYEVENTF_KEYDOWN = 0x00;

        //相当于按下ctrl+v，然后回车
        private void KeyBordPaste()
        {
            keybd_event(Keys.ControlKey, 0, KEYEVENTF_KEYDOWN, 0);
            keybd_event(Keys.V, 0, 0, 0);
            keybd_event(Keys.ControlKey, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(Keys.Enter, 0, 0, 0);
        }

    }
}


