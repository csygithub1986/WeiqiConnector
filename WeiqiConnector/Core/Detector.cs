using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Collections;
using System.Runtime.InteropServices;

namespace GoImageDetection.Core
{
    /// <summary>
    /// 检测图形算法
    /// 其中有2个假设：
    /// 1、标准棋盘为（0.92~0.96）:1，为了简便，这里图像像素强制为正方形输入。假定棋盘每一边都在(0.7~1) *(图宽)范围内
    /// 2、棋盘交叉点误差应该小于最大格宽的正负10%；圆心坐标相差应小于最大格宽的正负25%
    /// 目前采用整个棋盘全部检测模式
    /// </summary>
    public class Detector : IDetect
    {
        double minWidthRate = 0.8;
        double cannyThreshold = 220;   // 参数5：边缘检测阈值（30~180）
        double circleAccumulatorThreshold = 25;       // 参数6：累加器阈值（圆心重合点，越低的时候圆弧就越容易当成圆）
        double circleCannyThresh = 100;    // 圆的边缘检测阈值（30~180）

        #region 可调参数

        #region 十字检测
        double crossFillRate = 0.2;
        #endregion

        #endregion

        double dp = 1;    // 参数3：dp，不懂

        int maxGridWidth;//最大格宽
        public int minGridWidth;//最小格宽
        int crossDetectLen;//十字检测像素数(纵向)，也作为四周扫描的起点边界
        //int crossDetectWidth;//十字检测偏差像素数

        public CircleF[] Circles;
        public Dictionary<CrossType, List<Point>> CrossPoints;

        public UMat cannyEdges;
        //Bitmap bitmap;
        UMat grayImage;
        int boardSize;
        int imageWidth;
        int imageHeight;

        public PointF[] conors;
        public Point[] AllCoordinate;

        public Detector(double crossFillRate)
        {
            this.crossFillRate = crossFillRate;
        }

        /// <summary>
        /// 检测
        /// </summary>
        /// <param name="bitmap">图像，必须为正方形</param>
        /// <param name="boardSize">大小</param>
        /// <returns></returns>
        public int[] Detect(Bitmap bitmap, int boardSize, bool alreadyHasCoor = false)
        {
            this.boardSize = boardSize;
            UMat uimage = InitImage(bitmap);
            imageWidth = uimage.Cols;
            imageHeight = uimage.Rows;
            maxGridWidth = uimage.Size.Width / (boardSize - 1);
            minGridWidth = (int)(uimage.Size.Width / (boardSize - 1) * minWidthRate);
            crossDetectLen = minGridWidth / 4;

            cannyEdges = new UMat();
            //第三、四个参数分别为边缘检测阈值和连接阈值（大于第一个作为边界，小于第二个舍弃，介于之间时看该点是否连接着其他边界点）
            CvInvoke.Canny(uimage, cannyEdges, cannyThreshold, cannyThreshold * 0.8);
            if (alreadyHasCoor == false)
            {
                #region 1、找交点
                DateTime t1 = DateTime.Now;
                CrossPoints = DetectCross(cannyEdges.Bytes, cannyEdges.Cols);
                DateTime t2 = DateTime.Now;
                Console.WriteLine("DetectCross " + (t2 - t1).TotalMilliseconds + " ms");
                #endregion

                #region 2、找角
                PointF directionLeft = PointF.Empty;
                PointF directionRight = PointF.Empty;
                PointF directionUp = PointF.Empty;
                PointF directionDown = PointF.Empty;
                conors = FindConor(out directionLeft, out directionRight, out directionUp, out directionDown);
                DateTime t3 = DateTime.Now;
                Console.WriteLine("FindConor " + (t3 - t2).TotalMilliseconds + " ms");
                if (conors == null)
                {
                    return null;
                }
                #endregion

                #region 3、修正透视，计算网络
                LineSegment2DF[] horizontalLines = null;
                LineSegment2DF[] verticalLines = null;
                GetEvenDevideLines(conors, directionLeft, directionRight, directionUp, directionDown, out horizontalLines, out verticalLines);
                AllCoordinate = GetGridCoordinate(horizontalLines, verticalLines);
                DateTime t4 = DateTime.Now;
                Console.WriteLine("GetGridCoordinate " + (t4 - t3).TotalMilliseconds + " ms");
                #endregion
            }

            //找圆
            Circles = DetectCircle(uimage, boardSize, (int)((AllCoordinate[boardSize - 1].X - AllCoordinate[0].X) / (boardSize - 1)));

            #region 4、分析颜色
            int[] stones = new int[boardSize * boardSize];
            byte[] imageByte = cannyEdges.Bytes;//如果直接随机访问cannyEdges.Bytes中的元素，会非常耗时
            byte[] grayImageData = grayImage.Bytes;
            for (int i = 0; i < stones.Length; i++)
            {
                stones[i] = FindStone(i, imageByte, grayImageData);
            }
            #endregion

            return stones;
        }


        /// <summary>
        /// 图形预处理
        /// </summary>
        private UMat InitImage(Bitmap bitmap)
        {
            Image<Bgr, Byte> img = new Image<Bgr, byte>(bitmap);
            //转为灰度级图像
            grayImage = new UMat();
            CvInvoke.CvtColor(img, grayImage, ColorConversion.Rgb2Gray);
            //use image pyr to remove noise 降噪，为了更准确的做边缘检测
            //UMat pyrDown = new UMat();
            //CvInvoke.PyrDown(grayImage, pyrDown);
            //CvInvoke.PyrUp(pyrDown, grayImage);
            return grayImage;
        }

        /// <summary>
        /// 检测圆
        /// </summary>
        private CircleF[] DetectCircle(UMat uimage, int boardSize, int oneGridPixel)
        {
            ////棋子最大半径
            //int maxRadius = maxGridWidth / 2;//棋子最大宽度为格的二分之一
            ////棋子最小半径为最大半径*0.7
            //int minRadius = (int)(maxRadius * minWidthRate);
            ////最小间距为最小直径
            //int minDistance = minRadius * 2;

            int maxRadius = (int)(oneGridPixel * 1.3 / 2);
            int minRadius = (int)(oneGridPixel * 0.7 / 2);
            int minDistance = minRadius * 2;

            CircleF[] circles = CvInvoke.HoughCircles(uimage, HoughType.Gradient, dp, minDistance, circleCannyThresh, circleAccumulatorThreshold, minRadius, maxRadius);
            return circles;
        }


        /// <summary>
        /// 只检测四边的T型交叉点和四角的L型交叉点
        /// 从第一行开始扫描┌ ┐┬形状，从扫到第一个┬开始，再向下扫两个最小格宽。最多扫1/10个图形高度。
        /// 从底排扫描└ ┘┴形状，后面同上
        /// 从左边扫描├，从右边扫描┤，后面同理
        /// </summary>
        /// <param name="width"></param>
        /// <param name="imageBytes"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private Dictionary<CrossType, List<Point>> DetectCross(byte[] imageBytes, int width)
        {
            Dictionary<CrossType, List<Point>> crossDic = new Dictionary<CrossType, List<Point>>();
            int height = imageBytes.Length / width;
            int scanRowCount = height / 7;//最多扫描的行数
            int scanColCount = width / 7;
            //try
            //{
            //上 //TODO：优化，扫描到了以后就缩小扫描行数，或者扫描到19个了以后就结束，或者加上圆形一共有19个了就结束
            int edge = scanRowCount + crossDetectLen;
            bool first = true;

            for (int j = crossDetectLen; j < edge; j++)
            {
                for (int i = crossDetectLen; i < width - crossDetectLen; i++)
                {
                    if (imageBytes[i + j * width] == 255)
                    {
                        CrossType type = GetCrossFromUp(width, imageBytes, i, j);
                        if (type != CrossType.None)
                        {
                            AddPoint(crossDic, type, i, j);
                            if (first)
                            {
                                first = false;
                                edge = j + minGridWidth;
                            }
                            //并将之后的一片区域设为0，避免重复检查
                            for (int ii = 0; ii < crossDetectLen; ii++)
                            {
                                for (int jj = 0; jj < crossDetectLen; jj++)
                                {
                                    imageBytes[i + ii + (j + jj) * width] = 0;
                                }
                            }
                        }
                    }
                }
            }
            //下
            edge = height - scanRowCount - crossDetectLen;
            first = true;
            for (int j = height - 1 - crossDetectLen; j > edge; j--)
            {
                for (int i = crossDetectLen; i < width - crossDetectLen; i++)//用crossDetectLen作为两边缓冲区
                {
                    if (imageBytes[i + j * width] == 255)
                    {
                        CrossType type = GetCrossFromDown(width, imageBytes, i, j);
                        if (type != CrossType.None)
                        {
                            AddPoint(crossDic, type, i, j);
                            if (first)
                            {
                                first = false;
                                edge = j - minGridWidth;
                            }
                            //并将之后的一片区域设为0，避免重复检查
                            for (int ii = 0; ii < crossDetectLen; ii++)
                            {
                                for (int jj = 0; jj < crossDetectLen; jj++)
                                {
                                    imageBytes[i + ii + (j + jj) * width] = 0;
                                }
                            }
                        }
                    }
                }
            }
            //左
            edge = scanColCount + crossDetectLen;
            first = true;
            for (int i = crossDetectLen; i < edge; i++)
            {
                for (int j = crossDetectLen; j < height - crossDetectLen; j++)
                {
                    if (imageBytes[i + j * width] == 255)
                    {
                        CrossType type = GetCrossFromLeft(width, imageBytes, i, j);
                        if (type != CrossType.None)
                        {
                            AddPoint(crossDic, type, i, j);
                            if (first)
                            {
                                first = false;
                                edge = i + minGridWidth;
                            }
                            //并将之后的一片区域设为0，避免重复检查
                            for (int ii = 0; ii < crossDetectLen; ii++)
                            {
                                for (int jj = 0; jj < crossDetectLen; jj++)
                                {
                                    imageBytes[i + ii + (j + jj) * width] = 0;
                                }
                            }
                        }
                    }
                }
            }
            //右
            edge = width - scanColCount - crossDetectLen;
            first = true;
            for (int i = width - 1 - crossDetectLen; i > edge; i--)
            {
                for (int j = crossDetectLen; j < height - crossDetectLen; j++)
                {
                    if (imageBytes[i + j * width] == 255)
                    {
                        CrossType type = GetCrossFromRight(width, imageBytes, i, j);
                        if (type != CrossType.None)
                        {
                            AddPoint(crossDic, type, i, j);
                            if (first)
                            {
                                first = false;
                                edge = i - minGridWidth;
                            }
                            //并将之后的一片区域设为0，避免重复检查
                            for (int ii = 0; ii < crossDetectLen; ii++)
                            {
                                for (int jj = 0; jj < crossDetectLen; jj++)
                                {
                                    imageBytes[i + ii + (j + jj) * width] = 0;
                                }
                            }
                        }
                    }
                }
            }
            return crossDic;
            //}
            //catch (Exception ex)
            //{
            //    throw ex;
            //}
        }


        /// <summary>
        /// 从上检查交叉点
        /// </summary>
        /// <param name="width"></param>
        /// <param name="imageBytes"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>0表示非交叉点，1,2,3,4分别表示左上右下T字，5,6,7,8分别表示从左上角开始顺时针方向的角。所以在这里可能会有0,2,5,6四种情况</returns>
        private CrossType GetCrossFromUp(int width, byte[] imageBytes, int x, int y)
        {
            bool toDown = IsLineOnDirection(width, imageBytes, x, y, 0, 1);
            if (toDown == false)
            {
                return CrossType.None;
            }
            bool toUp = IsLineOnDirection(width, imageBytes, x, y, 0, -1);
            if (toUp)
            {
                return CrossType.None;
            }
            bool toLeft = IsLineOnDirection(width, imageBytes, x, y, -1, 0);
            bool toRight = IsLineOnDirection(width, imageBytes, x, y, 1, 0);
            if (toLeft && toRight)
            {
                return CrossType.Up;
            }
            if (toLeft && toRight == false)
            {
                return CrossType.RightUp;
            }
            if (toLeft == false && toRight)
            {
                return CrossType.LeftUp;
            }
            return CrossType.None;
        }

        /// <summary>
        /// 从下检查交叉点
        /// </summary>
        /// <param name="width"></param>
        /// <param name="imageBytes"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>0表示非交叉点，1,2,3,4分别表示左上右下T字，5,6,7,8分别表示从左上角开始顺时针方向的角。所以在这里可能会有0,4,7,8四种情况</returns>
        private CrossType GetCrossFromDown(int width, byte[] imageBytes, int x, int y)
        {
            bool toUp = IsLineOnDirection(width, imageBytes, x, y, 0, -1);
            if (toUp == false)
            {
                return CrossType.None;
            }
            bool toDown = IsLineOnDirection(width, imageBytes, x, y, 0, 1);
            if (toDown)
            {
                return CrossType.None;
            }
            bool toLeft = IsLineOnDirection(width, imageBytes, x, y, -1, 0);
            bool toRight = IsLineOnDirection(width, imageBytes, x, y, 1, 0);
            if (toLeft && toRight)
            {
                return CrossType.Down;
            }
            if (toLeft && toRight == false)
            {
                return CrossType.RightDown;
            }
            if (toLeft == false && toRight)
            {
                return CrossType.LeftDown;
            }
            return CrossType.None;
        }

        /// <summary>
        /// 从左检查交叉点
        /// </summary>
        /// <param name="width"></param>
        /// <param name="imageBytes"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>0表示非交叉点，1,2,3,4分别表示左上右下T字，5,6,7,8分别表示从左上角开始顺时针方向的角。所以在这里可能会有0,1两种情况</returns>
        private CrossType GetCrossFromLeft(int width, byte[] imageBytes, int x, int y)
        {
            bool toRight = IsLineOnDirection(width, imageBytes, x, y, 1, 0);
            if (toRight == false)
            {
                return CrossType.None;
            }
            bool toLeft = IsLineOnDirection(width, imageBytes, x, y, -1, 0);
            if (toLeft)
            {
                return CrossType.None;
            }
            bool toUp = IsLineOnDirection(width, imageBytes, x, y, 0, -1);
            if (toUp == false)
            {
                return CrossType.None;
            }
            bool toDown = IsLineOnDirection(width, imageBytes, x, y, 0, 1);
            if (toDown == false)
            {
                return CrossType.None;
            }
            return CrossType.Left;
        }

        /// <summary>
        /// 从右检查交叉点
        /// </summary>
        /// <param name="width"></param>
        /// <param name="imageBytes"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>0表示非交叉点，1,2,3,4分别表示左上右下T字，5,6,7,8分别表示从左上角开始顺时针方向的角。所以在这里可能会有0,3两种情况</returns>
        private CrossType GetCrossFromRight(int width, byte[] imageBytes, int x, int y)
        {
            bool toLeft = IsLineOnDirection(width, imageBytes, x, y, -1, 0);
            if (toLeft == false)
            {
                return CrossType.None;
            }
            bool toRight = IsLineOnDirection(width, imageBytes, x, y, 1, 0);
            if (toRight)
            {
                return CrossType.None;
            }
            bool toUp = IsLineOnDirection(width, imageBytes, x, y, 0, -1);
            if (toUp == false)
            {
                return CrossType.None;
            }
            bool toDown = IsLineOnDirection(width, imageBytes, x, y, 0, 1);
            if (toDown == false)
            {
                return CrossType.None;
            }
            return CrossType.Right;
        }

        private CrossType GetCrossFromCenter(int width, byte[] imageBytes, int x, int y)
        {
            bool toLeft = IsLineOnDirection(width, imageBytes, x, y, -1, 0);
            if (toLeft == false)
            {
                return CrossType.None;
            }
            bool toRight = IsLineOnDirection(width, imageBytes, x, y, 1, 0);
            if (toRight == false)
            {
                return CrossType.None;
            }
            bool toUp = IsLineOnDirection(width, imageBytes, x, y, 0, -1);
            if (toUp == false)
            {
                return CrossType.None;
            }
            bool toDown = IsLineOnDirection(width, imageBytes, x, y, 0, 1);
            if (toDown == false)
            {
                return CrossType.None;
            }
            return CrossType.Center;
        }


        /// <summary>
        /// 对于某点，判断某个方向上是否有直线
        /// </summary>
        /// <param name="width">图形的宽度</param>
        /// <param name="imageBytes">图形数据</param>
        /// <param name="x">要计算的点坐标x</param>
        /// <param name="y">要计算的点坐标y</param>
        /// <param name="directionX">x方向，取-1,0,1</param>
        /// <param name="directionY">y方向，取-1,0,1</param>
        /// <returns></returns>
        private bool IsLineOnDirection(int width, byte[] imageBytes, int x, int y, int directionX, int directionY)
        {
            //try
            //{
            int iMin = 0, iMax = 0, jMin = 0, jMax = 0;
            //根据要判断的方向，计算坐标
            if (directionX == 0)
            {
                iMin = -2;
                iMax = 2;
                if (directionY == 1)
                {
                    jMin = 1;
                    jMax = crossDetectLen;
                }
                else
                {
                    jMin = -crossDetectLen;
                    jMax = -1;
                }
            }
            else if (directionY == 0)
            {
                jMin = -2;
                jMax = 2;
                if (directionX == 1)
                {
                    iMin = 1;
                    iMax = crossDetectLen;
                }
                else
                {
                    iMin = -crossDetectLen;
                    iMax = -1;
                }
            }

            byte[] whiteBytes = new byte[5 * crossDetectLen];
            int index = 0;
            for (int i = iMin; i <= iMax; i++)
            {
                for (int j = jMin; j <= jMax; j++)
                {
                    if (x + i + (y + j) * width >= 0 && x + i + (y + j) * width < imageBytes.Length)
                    {
                        whiteBytes[index++] = imageBytes[x + i + (y + j) * width];
                    }
                }
            }
            int whiteCount = whiteBytes.Count(b => b == 255);
            rate = (double)whiteCount / whiteBytes.Length;
            return whiteCount > whiteBytes.Length * crossFillRate;
            //}
            //catch (Exception ex)
            //{
            //    throw ex;
            //}
        }

        private void AddPoint(Dictionary<CrossType, List<Point>> crossDic, CrossType type, int x, int y)
        {
            if (crossDic.Keys.Contains(type))
            {
                crossDic[type].Add(new Point(x, y));
            }
            else
            {
                List<Point> pList = new List<Point>();
                pList.Add(new Point(x, y));
                crossDic.Add(type, pList);
            }
        }

        public enum CrossType
        {
            None = 0, Left, Up, Right, Down, LeftUp, RightUp, RightDown, LeftDown, Center
        }

        #region canny和直线图

        /// <summary>
        /// 找到棋盘四个顶点
        /// 因为如果一边偏宽，那么它会使它两边的格子不均匀，所以顶点如果不方正，后面的找全部坐标考虑矫正，如果矫正效果不好，考虑放弃不方正的顶角。（待定）
        /// </summary>
        /// <returns>从左上角顺时针的点</returns>
        private PointF[] FindConor(out PointF directionLeft, out PointF directionRight, out PointF directionUp, out PointF directionDown)
        {
            directionLeft = PointF.Empty;
            directionRight = PointF.Empty;
            directionUp = PointF.Empty;
            directionDown = PointF.Empty;
            List<PointF> leftPoints = new List<PointF>();
            if (CrossPoints.Keys.Contains(CrossType.Left))
            {
                foreach (var item in CrossPoints[CrossType.Left])
                {
                    leftPoints.Add(new PointF(item.X, item.Y));
                }
            }
            if (leftPoints.Count < 2)
            {
                return null;
            }
            PointF pointOnLineLeft;
            LineMethods.LineFit(leftPoints.ToArray(), out directionLeft, out pointOnLineLeft);

            List<PointF> rightPoints = new List<PointF>();
            if (CrossPoints.Keys.Contains(CrossType.Right))
            {
                foreach (var item in CrossPoints[CrossType.Right])
                {
                    rightPoints.Add(new PointF(item.X, item.Y));
                }
            }
            if (rightPoints.Count < 2)
            {
                return null;
            }
            PointF pointOnLineRight;
            LineMethods.LineFit(rightPoints.ToArray(), out directionRight, out pointOnLineRight);

            List<PointF> upPoints = new List<PointF>();
            if (CrossPoints.Keys.Contains(CrossType.Up))
            {
                foreach (var item in CrossPoints[CrossType.Up])
                {
                    upPoints.Add(new PointF(item.X, item.Y));
                }
            }
            if (CrossPoints.Keys.Contains(CrossType.LeftUp))
            {
                foreach (var item in CrossPoints[CrossType.LeftUp])
                {
                    upPoints.Add(new PointF(item.X, item.Y));
                }
            }
            if (CrossPoints.Keys.Contains(CrossType.RightUp))
            {
                foreach (var item in CrossPoints[CrossType.RightUp])
                {
                    upPoints.Add(new PointF(item.X, item.Y));
                }
            }
            if (upPoints.Count < 2)
            {
                return null;
            }

            PointF pointOnLineUp;
            LineMethods.LineFit(upPoints.ToArray(), out directionUp, out pointOnLineUp);

            List<PointF> downPoints = new List<PointF>();
            if (CrossPoints.Keys.Contains(CrossType.Down))
            {
                foreach (var item in CrossPoints[CrossType.Down])
                {
                    downPoints.Add(new PointF(item.X, item.Y));
                }
            }
            if (CrossPoints.Keys.Contains(CrossType.LeftDown))
            {
                foreach (var item in CrossPoints[CrossType.LeftDown])
                {
                    downPoints.Add(new PointF(item.X, item.Y));
                }
            }
            if (CrossPoints.Keys.Contains(CrossType.RightDown))
            {
                foreach (var item in CrossPoints[CrossType.RightDown])
                {
                    downPoints.Add(new PointF(item.X, item.Y));
                }
            }
            if (downPoints.Count < 2)
            {
                return null;
            }

            PointF pointOnLineDown;
            LineMethods.LineFit(downPoints.ToArray(), out directionDown, out pointOnLineDown);//拟合

            //求交点
            PointF? leftTop = LineMethods.FindLineCross(directionLeft, pointOnLineLeft, directionUp, pointOnLineUp);
            PointF? rightTop = LineMethods.FindLineCross(directionRight, pointOnLineRight, directionUp, pointOnLineUp);
            PointF? leftDown = LineMethods.FindLineCross(directionLeft, pointOnLineLeft, directionDown, pointOnLineDown);
            PointF? rightDown = LineMethods.FindLineCross(directionRight, pointOnLineRight, directionDown, pointOnLineDown);
            if (leftTop == null || rightTop == null || leftDown == null || rightDown == null)
            {
                return null;
            }
            PointF[] result = new PointF[] { leftTop.Value, rightTop.Value, rightDown.Value, leftDown.Value };
            return result;
        }

        //通过四个角，矫正获得等分线
        private void GetEvenDevideLines(PointF[] conors, PointF directionLeft, PointF directionRight, PointF directionUp, PointF directionDown, out LineSegment2DF[] horizontalLines, out LineSegment2DF[] verticalLines)
        {
            PointF leftUpPoint = conors[0];
            PointF rightUpPoint = conors[1];
            PointF rightDownPoint = conors[2];
            PointF leftDownPoint = conors[3];

            PointF[] upPoints = null;
            PointF[] downPoints = null;
            PointF[] leftPoints = null;
            PointF[] rightPoints = null;

            horizontalLines = new LineSegment2DF[boardSize];
            verticalLines = new LineSegment2DF[boardSize];
            horizontalLines[0] = new LineSegment2DF(leftUpPoint, rightUpPoint);
            horizontalLines[boardSize - 1] = new LineSegment2DF(leftDownPoint, rightDownPoint);
            verticalLines[0] = new LineSegment2DF(leftUpPoint, leftDownPoint);
            verticalLines[boardSize - 1] = new LineSegment2DF(rightUpPoint, rightDownPoint);

            //分三种情况：1、都不平行。2、有一对平行。3、都平行
            //平行的判定条件，tanα <0.005
            float parallelAngle = 0.005f;

            float tanUp = directionUp.Y / directionUp.X; //已考虑除数不会为零
            float tanDown = directionDown.Y / directionDown.X;
            bool horizontalParallel = Math.Abs((tanUp - tanDown) / (1 + tanUp * tanDown)) < parallelAngle; //判断上下是否平行，根据三角公式tan(a-b)

            float tanLeft = directionLeft.X / directionLeft.Y;
            float tanRight = directionRight.X / directionRight.Y;
            bool verticalParallel = Math.Abs((tanLeft - tanRight) / (1 + tanLeft * tanRight)) < parallelAngle;  //判断左右是否平行

            if (horizontalParallel)
            {
                upPoints = new PointF[boardSize];
                for (int i = 0; i < boardSize; i++)
                {
                    upPoints[i] = new PointF();
                    upPoints[i].X = leftUpPoint.X + (rightUpPoint.X - leftUpPoint.X) * i / (boardSize - 1);
                    upPoints[i].Y = leftUpPoint.Y + (rightUpPoint.Y - leftUpPoint.Y) * i / (boardSize - 1);
                }
                downPoints = new PointF[boardSize];
                for (int i = 0; i < boardSize; i++)
                {
                    downPoints[i] = new PointF();
                    downPoints[i].X = leftDownPoint.X + (rightDownPoint.X - leftDownPoint.X) * i / (boardSize - 1);
                    downPoints[i].Y = leftDownPoint.Y + (rightDownPoint.Y - leftDownPoint.Y) * i / (boardSize - 1);
                }
                verticalLines = new LineSegment2DF[boardSize];
                for (int i = 0; i < boardSize; i++)
                {
                    verticalLines[i] = new LineSegment2DF(upPoints[i], downPoints[i]);
                }
            }
            if (verticalParallel)
            {
                leftPoints = new PointF[boardSize];
                for (int i = 0; i < boardSize; i++)
                {
                    leftPoints[i] = new PointF();
                    leftPoints[i].X = leftUpPoint.X + (leftDownPoint.X - leftUpPoint.X) * i / (boardSize - 1);
                    leftPoints[i].Y = leftUpPoint.Y + (leftDownPoint.Y - leftUpPoint.Y) * i / (boardSize - 1);
                }
                rightPoints = new PointF[boardSize];
                for (int i = 0; i < boardSize; i++)
                {
                    rightPoints[i] = new PointF();
                    rightPoints[i].X = rightUpPoint.X + (rightDownPoint.X - rightUpPoint.X) * i / (boardSize - 1);
                    rightPoints[i].Y = rightUpPoint.Y + (rightDownPoint.Y - rightUpPoint.Y) * i / (boardSize - 1);
                }
                horizontalLines = new LineSegment2DF[boardSize];
                for (int i = 0; i < boardSize; i++)
                {
                    horizontalLines[i] = new LineSegment2DF(leftPoints[i], rightPoints[i]);
                }
            }
            if (horizontalParallel && !verticalParallel)
            {
                leftPoints = new PointF[boardSize];
                rightPoints = new PointF[boardSize];
                leftPoints[0] = leftUpPoint;
                leftPoints[boardSize - 1] = leftDownPoint;
                rightPoints[0] = rightUpPoint;
                rightPoints[boardSize - 1] = rightDownPoint;
                //渐进作对角线，这样点在两边，会准确一点。如果boardsize(n)是偶数，n/2成为下部的第一条线，如果是奇数，n/2为中线
                //从上面画线时，都从1画到n/2-1，从下面画线时，从n-2画到n/2。当奇偶不同时，从下划线条数不同，但代码一致。
                for (int i = 0; i < boardSize / 2 - 1; i++)
                {
                    //从左上和右上开始，往对边画对角线，然后求平行的第1、2...n/2-1条线
                    LineSegment2DF diagonalLineUp1 = new LineSegment2DF(leftPoints[i], downPoints[boardSize - i - 1]);
                    LineSegment2DF diagonalLineUp2 = new LineSegment2DF(rightPoints[i], downPoints[i]);
                    //和第1,n-2条垂直线交点
                    PointF pLeft = (PointF)LineMethods.FindLineCross(diagonalLineUp1.Direction, diagonalLineUp1.P1, verticalLines[1].Direction, verticalLines[1].P1);
                    PointF pRight = (PointF)LineMethods.FindLineCross(diagonalLineUp2.Direction, diagonalLineUp2.P1, verticalLines[boardSize - 2].Direction, verticalLines[boardSize - 2].P1);
                    horizontalLines[i + 1] = new LineSegment2DF(pLeft, pRight);
                    leftPoints[i + 1] = (PointF)LineMethods.FindLineCross(horizontalLines[i + 1].Direction, pLeft, verticalLines[0].Direction, verticalLines[0].P1);
                    rightPoints[i + 1] = (PointF)LineMethods.FindLineCross(horizontalLines[i + 1].Direction, pRight, verticalLines[boardSize - 1].Direction, verticalLines[boardSize - 1].P1);
                }
                for (int i = boardSize - 1; i > boardSize / 2; i--)
                {
                    //从左下和右下开始，往对边画对角线，然后求平行的第n-2、n-3..n/2条线
                    LineSegment2DF diagonalLineDown1 = new LineSegment2DF(leftPoints[i], upPoints[i]);
                    LineSegment2DF diagonalLineDown2 = new LineSegment2DF(rightPoints[i], upPoints[boardSize - 1 - i]);
                    //和第1,n-2条垂直线交点
                    PointF pLeft = (PointF)LineMethods.FindLineCross(diagonalLineDown1.Direction, diagonalLineDown1.P1, verticalLines[1].Direction, verticalLines[1].P1);
                    PointF pRight = (PointF)LineMethods.FindLineCross(diagonalLineDown2.Direction, diagonalLineDown2.P1, verticalLines[boardSize - 2].Direction, verticalLines[boardSize - 2].P1);
                    horizontalLines[i - 1] = new LineSegment2DF(pLeft, pRight);
                    leftPoints[i - 1] = (PointF)LineMethods.FindLineCross(horizontalLines[i - 1].Direction, pLeft, verticalLines[0].Direction, verticalLines[0].P1);
                    rightPoints[i - 1] = (PointF)LineMethods.FindLineCross(horizontalLines[i - 1].Direction, pRight, verticalLines[boardSize - 1].Direction, verticalLines[boardSize - 1].P1);
                }
            }
            if (verticalParallel && !horizontalParallel)
            {
                upPoints = new PointF[boardSize];
                downPoints = new PointF[boardSize];
                upPoints[0] = leftUpPoint;
                upPoints[boardSize - 1] = rightUpPoint;
                downPoints[0] = leftDownPoint;
                downPoints[boardSize - 1] = rightDownPoint;
                //渐进作对角线，这样点在两边，会准确一点。如果boardsize(n)是偶数，n/2成为下部的第一条线，如果是奇数，n/2为中线
                //从上面画线时，都从1画到n/2-1，从下面画线时，从n-2画到n/2。当奇偶不同时，从下划线条数不同，但代码一致。
                for (int i = 0; i < boardSize / 2 - 1; i++)
                {
                    //从左上和右上开始，往对边画对角线，然后求平行的第1、2...n/2-1条线
                    LineSegment2DF diagonalLineLeft1 = new LineSegment2DF(upPoints[i], rightPoints[boardSize - 1 - i]);
                    LineSegment2DF diagonalLineLeft2 = new LineSegment2DF(downPoints[i], rightPoints[i]);
                    //和第1,n-2条平行线交点
                    PointF pUp = (PointF)LineMethods.FindLineCross(diagonalLineLeft1.Direction, diagonalLineLeft1.P1, horizontalLines[1].Direction, horizontalLines[1].P1);
                    PointF pDown = (PointF)LineMethods.FindLineCross(diagonalLineLeft2.Direction, diagonalLineLeft2.P1, horizontalLines[boardSize - 2].Direction, horizontalLines[boardSize - 2].P1);
                    verticalLines[i + 1] = new LineSegment2DF(pUp, pDown);
                    upPoints[i + 1] = (PointF)LineMethods.FindLineCross(verticalLines[i + 1].Direction, pUp, horizontalLines[0].Direction, horizontalLines[0].P1);
                    downPoints[i + 1] = (PointF)LineMethods.FindLineCross(verticalLines[i + 1].Direction, pDown, horizontalLines[boardSize - 1].Direction, horizontalLines[boardSize - 1].P1);
                }
                for (int i = boardSize - 1; i > boardSize / 2; i--)
                {
                    //从左下和右下开始，往对边画对角线，然后求平行的第n-2、n-3..n/2条线
                    LineSegment2DF diagonalLineRight1 = new LineSegment2DF(upPoints[i], leftPoints[i]);
                    LineSegment2DF diagonalLineRight2 = new LineSegment2DF(downPoints[i], leftPoints[boardSize - 1 - i]);
                    //和第1,n-2条垂直线交点
                    PointF pUp = (PointF)LineMethods.FindLineCross(diagonalLineRight1.Direction, diagonalLineRight1.P1, horizontalLines[1].Direction, horizontalLines[1].P1);
                    PointF pDown = (PointF)LineMethods.FindLineCross(diagonalLineRight2.Direction, diagonalLineRight2.P1, horizontalLines[boardSize - 2].Direction, horizontalLines[boardSize - 2].P1);
                    verticalLines[i - 1] = new LineSegment2DF(pUp, pDown);
                    upPoints[i - 1] = (PointF)LineMethods.FindLineCross(verticalLines[i - 1].Direction, pUp, horizontalLines[0].Direction, horizontalLines[0].P1);
                    downPoints[i - 1] = (PointF)LineMethods.FindLineCross(verticalLines[i - 1].Direction, pDown, horizontalLines[boardSize - 1].Direction, horizontalLines[boardSize - 1].P1);
                }
            }
            if (!horizontalParallel && !verticalParallel)
            {
                //1.寻找两个边的交点，及上平行线
                PointF horizontalCross = (PointF)LineMethods.FindLineCross(directionUp, leftUpPoint, directionDown, leftDownPoint);  //水平相交点
                PointF verticalCross = (PointF)LineMethods.FindLineCross(directionLeft, leftUpPoint, directionRight, rightUpPoint);  //垂直相交点
                LineSegment2DF l1 = new LineSegment2DF(horizontalCross, verticalCross);
                //2、过任意一点作l1的平行线 （找leftUpPoint和距leftUpPoint距离为1000的一点）
                PointF pointOnL2 = new PointF(leftUpPoint.X + 1000 * l1.Direction.X, leftUpPoint.Y + 1000 * l1.Direction.Y);
                LineSegment2DF l2 = new LineSegment2DF(leftUpPoint, pointOnL2);
                //3、让两边交于l2，并平分，作平分点和顶点交点连线，这些连线就是中间的格子线。
                //横端
                PointF end1 = (PointF)LineMethods.FindLineCross(horizontalLines[0].Direction, horizontalLines[0].P1, l2.Direction, l2.P1);
                PointF end2 = (PointF)LineMethods.FindLineCross(horizontalLines[boardSize - 1].Direction, horizontalLines[boardSize - 1].P1, l2.Direction, l2.P1);
                for (int i = 1; i < boardSize - 1; i++)
                {
                    PointF gridPoint = new PointF();
                    gridPoint.X = end1.X + (end2.X - end1.X) * i / (boardSize - 1);
                    gridPoint.Y = end1.Y + (end2.Y - end1.Y) * i / (boardSize - 1);
                    horizontalLines[i] = new LineSegment2DF(horizontalCross, gridPoint);
                }
                //竖端
                PointF end3 = (PointF)LineMethods.FindLineCross(verticalLines[0].Direction, verticalLines[0].P1, l2.Direction, l2.P1);
                PointF end4 = (PointF)LineMethods.FindLineCross(verticalLines[boardSize - 1].Direction, verticalLines[boardSize - 1].P1, l2.Direction, l2.P1);
                for (int i = 1; i < boardSize - 1; i++)
                {
                    PointF gridPoint = new PointF();
                    gridPoint.X = end3.X + (end4.X - end3.X) * i / (boardSize - 1);
                    gridPoint.Y = end3.Y + (end4.Y - end3.Y) * i / (boardSize - 1);
                    verticalLines[i] = new LineSegment2DF(verticalCross, gridPoint);
                }
                //妹的，怎么有一边平行的反而算法和代码更复杂
            }
        }

        private Point[] GetGridCoordinate(LineSegment2DF[] horizontalLines, LineSegment2DF[] verticalLines)
        {
            Point[] coordinates = new Point[boardSize * boardSize];
            for (int i = 0; i < boardSize; i++)
            {
                for (int j = 0; j < boardSize; j++)
                {
                    PointF pointf = (PointF)LineMethods.FindLineCross(verticalLines[i].Direction, verticalLines[i].P1, horizontalLines[j].Direction, horizontalLines[j].P1);
                    coordinates[i + j * boardSize] = new Point()
                    {
                        X = (int)pointf.X + 1,//因为检测的时候都偏小，这里补偿1像素
                        Y = (int)pointf.Y + 1//因为检测的时候都偏小，这里补偿1像素
                    };
                }
            }
            return coordinates;
        }


        //没有透视矫正前的求grid点算法，废弃了
        private Point[] CalculateAllCoordinate(PointF[] conors)
        {
            PointF leftTop = conors[0];
            PointF rightTop = conors[1];
            PointF rightDown = conors[2];
            PointF leftDown = conors[3];

            //先获得左右的等分
            PointF[] lefts = new PointF[boardSize];
            for (int i = 0; i < boardSize; i++)
            {
                lefts[i] = new PointF();
                lefts[i].X = leftTop.X + (leftDown.X - leftTop.X) * i / (boardSize - 1);
                lefts[i].Y = leftTop.Y + (leftDown.Y - leftTop.Y) * i / (boardSize - 1);
            }
            PointF[] rights = new PointF[boardSize];
            for (int i = 0; i < boardSize; i++)
            {
                rights[i] = new PointF();
                rights[i].X = rightTop.X + (rightDown.X - rightTop.X) * i / (boardSize - 1);
                rights[i].Y = rightTop.Y + (rightDown.Y - rightTop.Y) * i / (boardSize - 1);
            }

            //求所有
            Point[] coordinates = new Point[boardSize * boardSize];
            for (int i = 0; i < boardSize; i++)
            {
                for (int j = 0; j < boardSize; j++)
                {
                    coordinates[i + j * boardSize] = new Point()
                    {
                        X = (int)(lefts[j].X + (rights[j].X - lefts[j].X) * i / (boardSize - 1)) + 1,//因为检测的时候都偏小，这里补偿1像素
                        Y = (int)(lefts[j].Y + (rights[j].Y - lefts[j].Y) * i / (boardSize - 1)) + 1,//因为检测的时候都偏小，这里补偿1像素
                    };
                }
            }
            return coordinates;
        }

        /// <summary>
        /// 找棋子
        /// </summary>
        /// <returns>0:empty，1:black，2:white，-1:错误</returns>
        private int FindStone(int index, byte[] cannyBytes, byte[] grayImageData)
        {
            int indexX = index % boardSize;
            int indexY = index / boardSize;

            int x = AllCoordinate[index].X;
            int y = AllCoordinate[index].Y;
            //byte[] imageByte = cannyEdges.Bytes;//如果直接随机访问cannyEdges.Bytes中的元素，会非常耗时
            //下面三种方法，可信度从高到低
            #region 先找xy附近25%最大格宽是否有圆，然后圆内的灰度大于平均值（这里简单认为是128，或0.5）则是黑棋，小于则是白棋
            {
                int minX = x - maxGridWidth / 4;
                minX = minX < 0 ? 0 : minX;
                int maxX = x + maxGridWidth / 4;
                maxX = maxX >= imageWidth ? imageWidth - 1 : maxX;

                int minY = y - maxGridWidth / 4;
                minY = minY < 0 ? 0 : minY;
                int maxY = y + maxGridWidth / 4;
                maxY = maxY >= imageHeight ? imageHeight - 1 : maxY;

                CircleF circleStone = new CircleF(PointF.Empty, 0);
                foreach (var circle in Circles)
                {
                    //if (index==2)
                    //{

                    //}
                    if (circle.Center.X >= minX && circle.Center.X <= maxX && circle.Center.Y >= minY && circle.Center.Y <= maxY)
                    {
                        circleStone = circle;
                        break;
                    }
                }
                if (circleStone.Radius == 0)
                {
                    //或者以0.25倍最小格宽为半径的圆，百分之99（数值待定）都是黑色，判定为有圆
                    int totalCannyCount = 0;
                    int blackCount = 0;
                    float littleRadius = 0.25f * minGridWidth;
                    for (int i = (int)(-littleRadius) + 1; i < littleRadius; i++)
                    {
                        for (int j = (int)(-littleRadius) + 1; j < littleRadius; j++)
                        {
                            if (i * i + j * j < littleRadius * littleRadius)
                            {
                                if (x + i >= 0 && x + i < imageWidth && y + j >= 0 && y + j < imageHeight)
                                {
                                    totalCannyCount++;
                                    blackCount += cannyBytes[x + i + (y + j) * imageWidth] == 0 ? 1 : 0;
                                }
                            }
                        }
                    }
                    if ((float)blackCount / totalCannyCount >= 0.99)
                    {
                        circleStone = new CircleF(new PointF(x, y), littleRadius);
                        Console.Write("无圆但中心空洞   ");
                    }
                }
                else
                {
                    //Console.Write("有圆   ");
                }

                if (circleStone.Radius != 0)
                {
                    //求圆内灰度
                    float totalGray = 0;
                    int totalCount = 0;
                    for (int i = (int)(circleStone.Center.X - circleStone.Radius) + 1; i < circleStone.Center.X + circleStone.Radius; i++)
                    {
                        for (int j = (int)(circleStone.Center.Y - circleStone.Radius) + 1; j < circleStone.Center.Y + circleStone.Radius; j++)
                        {
                            if ((i - circleStone.Center.X) * (i - circleStone.Center.X) + (j - circleStone.Center.Y) * (j - circleStone.Center.Y) < circleStone.Radius * circleStone.Radius)
                            {
                                if (i >= 0 && i < imageWidth && j >= 0 && j < imageHeight)
                                {
                                    totalGray += grayImageData[i + j * imageWidth] / 255f;
                                    totalCount++;
                                }
                            }
                        }
                    }
                    float averageGray = totalGray / totalCount;
                    if (averageGray > 0.45)
                    {
                        //Console.Write("  为白" + "  灰度" + averageGray.ToString("F2"));
                        //Console.WriteLine("  (" + indexX + "," + indexY + ")");
                        return 2;//白
                    }
                    else if (averageGray < 0.45)
                    {
                        //Console.Write("  为黑" + "  灰度" + averageGray.ToString("F2"));
                        //Console.WriteLine("  (" + indexX + "," + indexY + ")");
                        return 1;//黑
                    }
                    else
                    {
                        Console.WriteLine("错误");
                        return -1;//错误
                    }
                }
                return 0;
            }
            #endregion

            //#region 再找xy附近10%最大格宽是否有十字，如果有，则为空
            //{
            //    int minX = x - maxGridWidth / 10;
            //    minX = minX < 0 ? 0 : minX;
            //    int maxX = x + maxGridWidth / 10;
            //    maxX = maxX >= imageWidth ? imageWidth - 1 : maxX;

            //    int minY = x - maxGridWidth / 10;
            //    minY = minY < 0 ? 0 : minY;
            //    int maxY = x + maxGridWidth / 10;
            //    maxY = maxY >= imageHeight ? imageHeight - 1 : maxY;

            //    for (int i = minX; i <= maxX; i++)
            //    {
            //        for (int j = minY; j <= maxY; j++)
            //        {
            //            CrossType type = GetCrossFromCenter(imageWidth, cannyBytes, i, j);
            //            if (type == CrossType.Center)
            //            {
            //                Console.Write("  找到十字");
            //                Console.WriteLine("  (" + indexX + "," + indexY + ")");
            //                return 0;//空
            //            }
            //        }
            //    }
            //}
            //#endregion

            //#region 如果既无圆也无十字，最后对0.4倍最小格宽为半径的圆求灰度，如果灰度<0.2或者>0.75（数值待定），之间则为空
            //{
            //    float totalGray = 0;
            //    int totalCount = 0;
            //    float littleRadius = 0.4f * minGridWidth;

            //    for (int i = (int)(-littleRadius) + 1; i < littleRadius; i++)
            //    {
            //        for (int j = (int)(-littleRadius) + 1; j < littleRadius; j++)
            //        {
            //            if (i * i + j * j < littleRadius * littleRadius)
            //            {
            //                if (x + i >= 0 && x + i < imageWidth && y + j >= 0 && y + j < imageHeight)
            //                {
            //                    totalCount++;
            //                    totalGray += grayImageData[x + i + (y + j) * imageWidth] / 255f;
            //                }
            //            }
            //        }
            //    }
            //    float averageGray = totalGray / totalCount;
            //    if (averageGray > 0.75)//白的灰度一般在0.6以上
            //    {
            //        Console.Write("  强行灰度为白" + "  灰度" + averageGray.ToString("F2"));
            //        Console.WriteLine("  (" + indexX + "," + indexY + ")");
            //        return 2;//白
            //    }
            //    else if (averageGray < 0.15)//黑的灰度一般在0.2以下
            //    {
            //        Console.Write("  强行灰度为黑" + "  灰度" + averageGray.ToString("F2"));
            //        Console.WriteLine("  (" + indexX + "," + indexY + ")");
            //        return 1;//黑
            //    }
            //    else
            //    {
            //        //Console.Write("  强行灰度为空");
            //        //Console.WriteLine("  (" + indexX + 1 + "," + indexY + 1 + ")");
            //        return 0;//空
            //    }
            //}
            //#endregion
        }
        #endregion

        #region 验证
        double rate;
        /// <summary>
        ///  验证十字
        /// </summary>
        /// <param name="x">坐标x</param>
        /// <param name="y">坐标y</param>
        /// <returns>左上右下的比例</returns>
        public double[] CheckCross(int x, int y)
        {
            if (x < crossDetectLen || x >= cannyEdges.Cols - crossDetectLen || y < crossDetectLen || y >= cannyEdges.Rows - crossDetectLen)
            {
                return null;
            }
            double[] rates = new double[4];
            //左
            IsLineOnDirection(imageWidth, cannyEdges.Bytes, x, y, -1, 0);
            rates[0] = rate;
            //上
            IsLineOnDirection(imageWidth, cannyEdges.Bytes, x, y, 0, -1);
            rates[1] = rate;
            //右
            IsLineOnDirection(imageWidth, cannyEdges.Bytes, x, y, 1, 0);
            rates[2] = rate;
            //下
            IsLineOnDirection(imageWidth, cannyEdges.Bytes, x, y, 0, 1);
            rates[3] = rate;
            return rates;
        }


        #endregion
    }
}
