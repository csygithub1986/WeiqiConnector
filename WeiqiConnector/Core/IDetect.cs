using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace GoImageDetection.Core
{
    public interface IDetect
    {
        /// <summary>
        /// 检测棋盘图像
        /// </summary>
        /// <param name="bitmap">图像</param>
        /// <param name="boradSize">棋盘路数，5~19</param>
        /// <returns>返回一维的整型数组表示的棋盘状态，坐标以左上角为(x=0,y=0)，第二个元素下标为(1,0)，即向右移动。元素值0为空，1为白棋，2为黑棋</returns>
        int[] Detect(Bitmap bitmap, int boradSize, bool alreadyHasCoor = false);
    }
}
