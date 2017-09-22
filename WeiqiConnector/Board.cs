using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WeiqiConnector
{
    public class Board
    {
        public Board(int size, string name)
        {
            _size = size;
            Name = name;
            BoardState = new int[19 * 19];
        }

        public Point ImagePoint1 { get; set; }
        public Point ImagePoint2 { get; set; }

        public Point BoardPoint1 { get; set; }
        public Point BoardPoint2 { get; set; }

        //public Point LastPoint { get; set; }
        //public Bitmap LastBitmap { get; set; }

        /// <summary>
        /// 棋盘状态
        /// </summary>
        public int[] BoardState { get; set; }

        public string Name { get; set; }

        private int _size;

        public Point[] AllCoordinate { get; set; }


        /// <summary>
        /// 根据鼠标点击的位置，计算落子的棋盘坐标（0~18）
        /// </summary>
        /// <param name="mousePoint">相对于image的位置（不是相对board位置）</param>
        /// <returns></returns>
        //public Point GetIndex(Point mousePoint)
        //{
        //    double portionX = (double)(BoardPoint2.X - BoardPoint1.X) / (_size - 1);
        //    int offsetX = mousePoint.X - (BoardPoint1.X - ImagePoint1.X);
        //    int x = (int)(offsetX / portionX);
        //    double dx = offsetX - x * portionX;
        //    if (dx > portionX / 2)
        //    {
        //        x++;
        //    }

        //    double portionY = (double)(BoardPoint2.Y - BoardPoint1.Y) / (_size - 1);
        //    int offsetY = mousePoint.Y - (BoardPoint1.Y - ImagePoint1.Y);
        //    int y = (int)(offsetY / portionY);
        //    double dy = offsetY - y * portionY;
        //    if (dy > portionY / 2)
        //    {
        //        y++;
        //    }
        //    Console.WriteLine(string.Format("{0}，检测到index：{1}", Name, new Point(x, y)));

        //    new Thread(() =>
        //    {
        //        LastBitmap.Save("C:\\Users\\Csy\\Desktop\\abc\\" + a.ToString() + ".bmp");
        //        a++;
        //    }).Start();

        //    return new Point(x, y);
        //}

        //static int a;

        /// <summary>
        /// 根据棋盘坐标（0~18），计算落子位置（相对于image）
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Point GetPosition(Point index)
        {
            double portionX = (double)(BoardPoint2.X - BoardPoint1.X) / (_size - 1);
            double portionY = (double)(BoardPoint2.Y - BoardPoint1.Y) / (_size - 1);

            int x = (int)(index.X * portionX) + (BoardPoint1.X - ImagePoint1.X);
            int y = (int)(index.Y * portionY) + (BoardPoint1.Y - ImagePoint1.Y);
            Console.WriteLine(string.Format("{0}，落子到{1}", Name, new Point(x, y)));
            return new Point(x, y);
        }
    }
}
