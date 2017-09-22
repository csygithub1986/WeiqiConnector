using AdrHook;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using GoImageDetection.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

namespace WeiqiConnector
{
    /// <summary>
    /// 注意：把启动后先落子的棋盘作为board1，先对他扫描
    /// </summary>
    public partial class MainWindow : Window
    {
        Board _board1;
        Board _board2;
        GameState _gameState = GameState.已停止;
        System.Threading.Timer _timer;
        int _turn;

        public CircleF[] _Circles;
        UMat _cannyEdges;
        Bitmap _bitmap;

        public MainWindow()
        {
            InitializeComponent();
            HookManager.KeyDown += Hook_KeyDown;
            HookManager.MouseDown += Hook_MouseDown;
            _board1 = new Board(19, "BoardA");
            _board2 = new Board(19, "BoardB");
            Title = _gameState.ToString();
        }

        private void Hook_MouseDown(object sender, MouseEventArgs e)
        {
            //Ctrl+鼠标右键 准备工作
            if ((int)System.Windows.Forms.Control.ModifierKeys == (int)Keys.Control && e.Button == MouseButtons.Right)
            {
                switch (_gameState)
                {
                    case GameState.已停止:
                        _board1.ImagePoint1 = e.Location;
                        _gameState = GameState.等待棋盘1坐标2;
                        Console.WriteLine(string.Format("image1坐标：{0}", e.Location));
                        break;
                    case GameState.等待棋盘1坐标2:
                        _board1.ImagePoint2 = e.Location;
                        _gameState = GameState.等待棋盘2坐标1;
                        break;
                    case GameState.等待棋盘2坐标1:
                        _board2.ImagePoint1 = e.Location;
                        _gameState = GameState.等待棋盘2坐标2;
                        break;
                    case GameState.等待棋盘2坐标2:
                        _board2.ImagePoint2 = e.Location;

                        //解析棋盘
                        {
                            int width = _board1.ImagePoint2.X - _board1.ImagePoint1.X;
                            int height = _board1.ImagePoint2.Y - _board1.ImagePoint1.Y;
                            Bitmap image1 = new Bitmap(width, height);
                            Graphics g = Graphics.FromImage(image1);
                            g.CopyFromScreen(_board1.ImagePoint1, new System.Drawing.Point(0, 0), new System.Drawing.Size(width, height));
                            g.Dispose();
                            Detector detector = new Detector(0.3);
                            int[] result = detector.Detect(image1, 19);
                            _board1.BoardPoint1 = new System.Drawing.Point((int)detector.conors[0].X + _board1.ImagePoint1.X, (int)detector.conors[0].Y + _board1.ImagePoint1.Y);
                            _board1.BoardPoint2 = new System.Drawing.Point((int)detector.conors[2].X + _board1.ImagePoint1.X, (int)detector.conors[2].Y + _board1.ImagePoint1.Y);
                            _board1.AllCoordinate = detector.AllCoordinate;
                            _board1.BoardState = result;
                        }
                        {
                            int width = _board2.ImagePoint2.X - _board2.ImagePoint1.X;
                            int height = _board2.ImagePoint2.Y - _board2.ImagePoint1.Y;
                            Bitmap image1 = new Bitmap(width, height);
                            Graphics g = Graphics.FromImage(image1);
                            g.CopyFromScreen(_board2.ImagePoint1, new System.Drawing.Point(0, 0), new System.Drawing.Size(width, height));
                            g.Dispose();
                            Detector detector = new Detector(0.3);
                            int[] result = detector.Detect(image1, 19);
                            _board2.BoardPoint1 = new System.Drawing.Point((int)detector.conors[0].X + _board2.ImagePoint1.X, (int)detector.conors[0].Y + _board2.ImagePoint1.Y);
                            _board2.BoardPoint2 = new System.Drawing.Point((int)detector.conors[2].X + _board2.ImagePoint1.X, (int)detector.conors[2].Y + _board2.ImagePoint1.Y);
                            _board2.AllCoordinate = detector.AllCoordinate;
                            _board2.BoardState = result;
                        }
                        _gameState = GameState.准备开始;
                        break;
                }
                Title = _gameState.ToString();
            }
        }
        private void Hook_KeyDown(object sender, KeyEventArgs e)
        {
            //S键启动或停止
            if (e.KeyValue == (int)Keys.S)
            {
                if (_gameState == GameState.准备开始 || _gameState == GameState.已停止)
                {
                    _gameState = GameState.已开始;
                    Start();
                }
                else if (_gameState == GameState.已开始)
                {
                    _gameState = GameState.已停止;
                    Stop();
                }
                Title = _gameState.ToString();
            }
            else if (e.KeyValue == (int)Keys.Q)
            {
                Mat circleImage = new Mat(_cannyEdges.Size, DepthType.Cv8U, 3);
                //circleImage.SetTo(new MCvScalar(0));
                CvInvoke.CvtColor(_cannyEdges, circleImage, ColorConversion.Gray2Bgr);

                foreach (CircleF circle in _Circles)
                    CvInvoke.Circle(circleImage, System.Drawing.Point.Round(circle.Center), (int)circle.Radius, new Bgr(System.Drawing.Color.Brown).MCvScalar, 2);
                image.Image = circleImage;
                Image<Bgr, Byte> img = new Image<Bgr, byte>(_bitmap);
                image2.Image = img;
            }

            if (e.KeyValue == (int)Keys.D1)
            {
                //Scan(0);
            }
            if (e.KeyValue == (int)Keys.D2)
            {
                //Scan(1);
            }
        }

        private void Start()
        {
            _timer = new System.Threading.Timer(CallBack, null, 0, 2000);
        }

        private void CallBack(object state)
        {
            Board board = _turn == 0 ? _board1 : _board2;
            int width = board.ImagePoint2.X - board.ImagePoint1.X;
            int height = board.ImagePoint2.Y - board.ImagePoint1.Y;
            Bitmap image = new Bitmap(width, height);
            Graphics g = Graphics.FromImage(image);
            g.CopyFromScreen(board.ImagePoint1, new System.Drawing.Point(0, 0), new System.Drawing.Size(width, height));
            g.Dispose();
            //if (board.LastBitmap == null)
            //{
            //    board.LastBitmap = image;
            //    return;
            //}

            //System.Drawing.Point point = GetNewPoint(board.LastBitmap, image, board);
            //if (point.IsEmpty)
            //{
            //    return;
            //}

            //扫描图像
            Detector detector = new Detector(0.3);
            detector.AllCoordinate = board.AllCoordinate;
            int[] boardState = detector.Detect(image, 19, true);
            _Circles = detector.Circles;
            _cannyEdges = detector.cannyEdges;
            _bitmap = image;
            if (boardState == null)
            {
                System.Windows.MessageBox.Show("检测不到棋盘");
                return;
            }

            int count = 0;
            int position = 0;
            //扫描到，和以前对比
            for (int i = 0; i < boardState.Length; i++)
            {
                if (board.BoardState[i] == 0 && 0 != boardState[i])
                {
                    count++;
                    position = i;
                }
            }
            Log("扫描" + board.Name + " 圆圈数：" + boardState.Count(p => p != 0));

            if (count > 1)
            {
                Log("检测到多于一个的不同圆");
                //Stop();
                return;
            }
            if (count == 0)
            {
                return;
            }
            board.BoardState = boardState;
            _turn = 1 - _turn;
            _timer.Dispose();//停止扫描

            //点击另一个board
            Board otherBoard = _turn == 0 ? _board1 : _board2;
            System.Drawing.Point index = new System.Drawing.Point(position % 19, position / 19);   //棋盘坐标
            System.Drawing.Point otherPoint = otherBoard.GetPosition(index);
            MouseKeyBoardController.mouse_event(MouseKeyBoardController.MOUSEEVENTF_ABSOLUTE | MouseKeyBoardController.MOUSEEVENTF_MOVE,
                (otherPoint.X + otherBoard.ImagePoint1.X) * 65535 / Screen.PrimaryScreen.Bounds.Width,
                (otherPoint.Y + otherBoard.ImagePoint1.Y) * 65535 / Screen.PrimaryScreen.Bounds.Height, 0, 0);
            Thread.Sleep(200);
            MouseKeyBoardController.mouse_event(MouseKeyBoardController.MOUSEEVENTF_ABSOLUTE | MouseKeyBoardController.MOUSEEVENTF_LEFTDOWN,
                otherPoint.X * 65535 / Screen.PrimaryScreen.Bounds.Width,
                otherPoint.Y * 65535 / Screen.PrimaryScreen.Bounds.Height, 0, 0);
            Thread.Sleep(100);
            MouseKeyBoardController.mouse_event(MouseKeyBoardController.MOUSEEVENTF_ABSOLUTE | MouseKeyBoardController.MOUSEEVENTF_LEFTUP,
                otherPoint.X * 65535 / Screen.PrimaryScreen.Bounds.Width,
                otherPoint.Y * 65535 / Screen.PrimaryScreen.Bounds.Height, 0, 0);
            Thread.Sleep(500);
            MouseKeyBoardController.mouse_event(MouseKeyBoardController.MOUSEEVENTF_ABSOLUTE | MouseKeyBoardController.MOUSEEVENTF_LEFTDOWN,
              otherPoint.X * 65535 / Screen.PrimaryScreen.Bounds.Width,
              otherPoint.Y * 65535 / Screen.PrimaryScreen.Bounds.Height, 0, 0);
            Thread.Sleep(100);
            MouseKeyBoardController.mouse_event(MouseKeyBoardController.MOUSEEVENTF_ABSOLUTE | MouseKeyBoardController.MOUSEEVENTF_LEFTUP,
                otherPoint.X * 65535 / Screen.PrimaryScreen.Bounds.Width,
                otherPoint.Y * 65535 / Screen.PrimaryScreen.Bounds.Height, 0, 0);
            Thread.Sleep(500);//点击以后延迟一段时间再扫描


            otherBoard.BoardState = boardState;//让另一个棋盘状态更新

            _timer = new System.Threading.Timer(CallBack, null, 500, 2000);//重新开始扫描
        }

        private void Stop()
        {
            _timer.Dispose();
            _timer = null;
        }

        enum GameState
        {
            已停止,//等待图像1坐标1
            等待棋盘1坐标1,
            等待棋盘1坐标2,
            等待棋盘2坐标1,
            等待棋盘2坐标2,
            准备开始,
            已开始
        }

        private void Log(string msg)
        {
            Dispatcher.BeginInvoke(
              new Action(() =>
              {
                  listbox.Items.Clear();
                  listbox.Items.Add(msg);
                  //if (listbox.Items.Count > 10)
                  //{
                  //    listbox.Items.RemoveAt(0);
                  //}
              }));
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            HookManager.KeyDown -= Hook_KeyDown;
            HookManager.MouseDown -= Hook_MouseDown;
            Environment.Exit(0);
        }
    }
}
