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
    //直线相关方法
    public class LineMethods
    {
        public static void LineFit(PointF[] points, out PointF direction, out PointF pointOnLine)
        {
            CvInvoke.FitLine(points, out direction, out pointOnLine, DistType.L2, 0, 0.01, 0.01);

        }

        /// <summary>
        /// 计算直线交点
        /// </summary>
        /// <param name="direction1"></param>
        /// <param name="pointOnLine1"></param>
        /// <param name="direction2"></param>
        /// <param name="pointOnLine2"></param>
        /// <returns></returns>
        public static PointF? FindLineCross(PointF direction1, PointF pointOnLine1, PointF direction2, PointF pointOnLine2)
        {
            if (direction1.X * direction2.Y == direction1.Y * direction2.X)//平行
            {
                return null;
            }
            float x, y;
            x = (direction1.X * direction2.X * (pointOnLine2.Y - pointOnLine1.Y) + direction1.Y * direction2.X * pointOnLine1.X - direction1.X * direction2.Y * pointOnLine2.X) / (direction2.X * direction1.Y - direction1.X * direction2.Y);
            y = (direction1.Y * direction2.Y * (pointOnLine2.X - pointOnLine1.X) + direction1.X * direction2.Y * pointOnLine1.Y - direction1.Y * direction2.X * pointOnLine2.Y) / (direction2.Y * direction1.X - direction1.Y * direction2.X);
            return new PointF(x, y);
        }
    }
}
