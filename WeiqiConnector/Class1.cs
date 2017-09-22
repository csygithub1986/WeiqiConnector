using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace WeiqiConnector
{
    class Class1
    {
        public void Fun()
        {
            //Bitmap bit = new Bitmap(100,100);
            Bitmap b = new Bitmap("test.bmp ");
            MemoryStream ms = new MemoryStream();
            b.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            byte[] bytes = ms.GetBuffer();  //byte[]   bytes=   ms.ToArray(); 这两句都可以，至于区别么，下面有解释
            ms.Close();
        }
        public static byte[] Bitmap2Byte(Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.MemoryBmp);
                byte[] data = new byte[stream.Length];
                stream.Seek(0, SeekOrigin.Begin);
                stream.Read(data, 0, Convert.ToInt32(stream.Length));
                Console.WriteLine(stream.Length);
                return data;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="originalData"></param>
        /// <param name="targetData"></param>
        /// <param name="width"></param>
        /// <param name="ignoreDistance">距离中心点大于ignoreDistance分之一宽度或高度的点将被忽略</param>
        /// <returns></returns>
        private Point GetDifferenceCenterPoint(byte[] originalData, byte[] targetData, int width, int ignoreDistance = 20)
        {
            return new Point();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="image1"></param>
        /// <param name="image2"></param>
        /// <param name="ignoreDistance">距离中心点大于ignoreDistance分之一宽度或高度的点将被忽略</param>
        /// <returns></returns>
        public static Point GetDifferenceCenterPoint(Bitmap image1, Bitmap image2, int ignoreDistance = 20)
        {

            List<Point> pointList = new List<Point>();
            for (int i = 0; i < image1.Width; i++)
            {
                for (int j = 0; j < image1.Height; j++)
                {
                    Color color1 = image1.GetPixel(i, j);
                    float b1 = color1.GetBrightness();
                    Color color2 = image2.GetPixel(i, j);
                    float b2 = color2.GetBrightness();
                    //if (!image1.GetPixel(i, j).Equals(image2.GetPixel(i, j)))
                    if (Math.Abs(b1 - b2) > 0.1)
                    {
                        pointList.Add(new Point(i, j));
                        //Console.WriteLine("Position:(" + i + "," + j + ")，Color:");// + image1.GetPixel(i, j).ToKnownColor() + "," + image2.GetPixel(i, j).ToKnownColor());
                    }
                }
            }
            int avgX = (int)pointList.Average(p => p.X);
            int avgY = (int)pointList.Average(p => p.Y);
            return new Point(avgX, avgY);
        }
        public static Bitmap CreateGrayscaleImage(int width, int height)
        {
            // create new image
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
            // set palette to grayscale
            SetGrayscalePalette(bmp);
            // return new image
            return bmp;
        }//#
        public static Bitmap RGB2Gray(Bitmap srcBitmap)
        {
            int wide = srcBitmap.Width;
            int height = srcBitmap.Height;
            Rectangle rect = new Rectangle(0, 0, wide, height);
            //将Bitmap锁定到系统内存中,获得BitmapData
            BitmapData srcBmData = srcBitmap.LockBits(rect,
                      ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            //创建Bitmap
            Bitmap dstBitmap = CreateGrayscaleImage(wide, height);//这个函数在后面有定义
            BitmapData dstBmData = dstBitmap.LockBits(rect,
                      ImageLockMode.ReadWrite, PixelFormat.Format8bppIndexed);
            //位图中第一个像素数据的地址。它也可以看成是位图中的第一个扫描行
            System.IntPtr srcPtr = srcBmData.Scan0;
            System.IntPtr dstPtr = dstBmData.Scan0;
            //将Bitmap对象的信息存放到byte数组中
            int src_bytes = srcBmData.Stride * height;
            byte[] srcValues = new byte[src_bytes];
            int dst_bytes = dstBmData.Stride * height;
            byte[] dstValues = new byte[dst_bytes];
            //复制GRB信息到byte数组
            System.Runtime.InteropServices.Marshal.Copy(srcPtr, srcValues, 0, src_bytes);
            System.Runtime.InteropServices.Marshal.Copy(dstPtr, dstValues, 0, dst_bytes);
            //根据Y=0.299*R+0.114*G+0.587B,Y为亮度
            for (int i = 0; i < height; i++)
                for (int j = 0; j < wide; j++)
                {
                    //只处理每行中图像像素数据,舍弃未用空间
                    //注意位图结构中RGB按BGR的顺序存储
                    int k = 3 * j;
                    byte temp = (byte)(srcValues[i * srcBmData.Stride + k + 2] * .299
                         + srcValues[i * srcBmData.Stride + k + 1] * .587
+ srcValues[i * srcBmData.Stride + k] * .114);
                    dstValues[i * dstBmData.Stride + j] = temp;
                }
            System.Runtime.InteropServices.Marshal.Copy(dstValues, 0, dstPtr, dst_bytes);
            //解锁位图
            srcBitmap.UnlockBits(srcBmData);
            dstBitmap.UnlockBits(dstBmData);
            return dstBitmap;
        }
        public static void SetGrayscalePalette(Bitmap srcImg)
        {
            // check pixel format
            if (srcImg.PixelFormat != PixelFormat.Format8bppIndexed)
                throw new ArgumentException();
            // get palette
            ColorPalette cp = srcImg.Palette;
            // init palette
            for (int i = 0; i < 256; i++)
            {
                cp.Entries[i] = Color.FromArgb(i, i, i);
            }
            srcImg.Palette = cp;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="byte1"></param>
        /// <param name="byte2"></param>
        /// <param name="lastPoint"></param>
        /// <param name="qiziCount">图像一排能容纳多少个棋子，默认20</param>
        /// <returns></returns>
        //public static Point GetDifferenceCenterPoint2(byte[,] byte1, byte[,] byte2, Board scanBoard, int qiziCount = 20)
        //{
        //    bool debug = true;
        //    int safeDistance = byte1.GetUpperBound(0) / qiziCount / 2;
        //    List<Point> pointList = new List<Point>();
        //    for (int j = 0; j <= byte1.GetUpperBound(0); j++)
        //    {
        //        for (int i = 0; i <= byte1.GetUpperBound(1); i++)
        //        {
        //            byte b1 = byte1[j, i];
        //            byte b2 = byte2[j, i];
        //            if (Math.Abs(b1 - b2) > 20 && //灰度差要大于50才算不同
        //                (Math.Abs(scanBoard.LastPoint.X - i) > safeDistance || //屏蔽掉以前棋子上的落子图标
        //                Math.Abs(scanBoard.LastPoint.Y - j) > safeDistance))
        //            {
        //                pointList.Add(new Point(i, j));
        //                if (debug)
        //                {
        //                    //Console.WriteLine(string.Format("测到不同点{0}，灰度差{1}", scanBoard.GetIndex(new Point(j, i)), Math.Abs(b1 - b2)));
        //                    debug = false;
        //                }
        //            }
        //        }
        //    }
        //    if (pointList.Count < 500)//少于500点
        //    {
        //        //Console.WriteLine(string.Format("扫描{0}，点数{1}", scanBoard.Name, pointList.Count));
        //        return Point.Empty;
        //    }
        //    int avgX = (int)pointList.Average(p => p.X);
        //    int avgY = (int)pointList.Average(p => p.Y);
        //    return new Point(avgX, avgY);
        //}





        //public static List<Point> GetDifferenceCenterPoint3(byte[,] byte1, byte[,] byte2, Board scanBoard, int qiziCount = 20)
        //{
        //    bool debug = false;
        //    int safeDistance = byte1.GetUpperBound(0) / qiziCount / 2;
        //    List<Point> pointList = new List<Point>();
        //    for (int j = 0; j <= byte1.GetUpperBound(0); j++)//0表示中括号中靠右的，即Y
        //    {
        //        for (int i = 0; i <= byte1.GetUpperBound(1); i++)
        //        {
        //            byte b1 = byte1[j, i];
        //            byte b2 = byte2[j, i];
        //            if (Math.Abs(b1 - b2) > 20 && //灰度差要大于50才算不同
        //                (Math.Abs(scanBoard.LastPoint.X - i) > safeDistance || //屏蔽掉以前棋子上的落子图标
        //                Math.Abs(scanBoard.LastPoint.Y - j) > safeDistance))
        //            {
        //                pointList.Add(new Point(i, j));
        //                if (debug)
        //                {
        //                    debug = false;
        //                }
        //            }
        //        }
        //    }
        //    return pointList;
        //}
    }
}
