﻿using BZ10.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace BZ10
{
    public class TransferClient
    {
        public class StateObject
        {
            public Socket workSocket = null;
            public const int BUFFER_SIZE = 1024;
            public byte[] buffer = new byte[BUFFER_SIZE];
            public StringBuilder sb = new StringBuilder();
        }

        //全局参数
        private System.Timers.Timer myTimer = null;
        public static DateTime newDateTime = DateTime.Parse("1970-01-01 00:00:00");//记录最新的从服务器接受到的数据时间
        private static int inteval = 30000; //心跳间隔
        private static MainForm form;//主界面对话框
        public Socket clientSocket = null; //客户端Socket
        public GlobalParam global = null; //全局参数
        public TransferClient(MainForm form1, GlobalParam param)
        {
            form = (MainForm)form1;
            global = param;
        }
        /// <summary>
        /// 心跳计时器初始化
        /// </summary>
        public void InitTimer()
        {
            try
            {
                if (myTimer == null)
                {
                    myTimer = new System.Timers.Timer(inteval);
                    myTimer.Elapsed += new System.Timers.ElapsedEventHandler(OnTimedEvent);
                    myTimer.Interval = inteval;
                }
            }
            catch (Exception ex)
            {

                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }
        }

        /// <summary>
        /// 定时发送心跳帧
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void OnTimedEvent(object source, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (clientSocket == null || newDateTime.AddMinutes(5) < DateTime.Now || !clientSocket.Connected)
                {
                    DebOutPut.DebLog("检测到与传输数据服务器通讯异常，设备将主动断开，并重新连接！");
                    DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "检测到与传输数据服务器通讯异常，设备将主动断开，并重新连接！");
                    myTimer.Enabled = false;
                    DebOutPut.DebLog("传输数据心跳帧终止！");
                    DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "传输数据心跳帧终止！");
                    closeSocket();
                    Thread.Sleep(60000);
                    connectToSever();
                }
                else if (clientSocket != null && clientSocket.Connected)
                {
                    sendKeepLive();
                }
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }
        }

        /// <summary>
        /// 连接服务器
        /// </summary>
        /// <returns></returns>
        public void connectToSever()
        {
            try
            {
                //连接服务器
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                clientSocket.Connect(global.host, global.port);
                if (clientSocket.Connected)
                {
                    newDateTime = DateTime.Now;
                    DebOutPut.DebLog("与传输数据服务器连接成功！");
                    DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "与传输数据服务器连接成功！");
                    Receive(clientSocket);
                }
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog("与传输数据服务器连接失败:" + ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, "与传输数据服务器连接失败:" + ex.ToString());
            }
            finally
            {
                //开启定时器，定时发送心跳帧
                if (myTimer == null)
                {
                    InitTimer();
                    myTimer.Enabled = true;
                    DebOutPut.DebLog("传输数据心跳帧启动！");
                    DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "传输数据心跳帧启动！");
                }
                else if (myTimer.Enabled == false)
                {
                    myTimer.Enabled = true;
                    DebOutPut.DebLog("传输数据心跳帧启动！");
                    DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "传输数据心跳帧启动！");
                }

            }
        }

        /// <summary>
        /// 接收
        /// </summary>
        /// <param name="client"></param>
        private void Receive(Socket client)
        {
            try
            {
                if (client != null && client.Connected)
                {
                    StateObject state = new StateObject();
                    state.workSocket = client;
                    client.BeginReceive(state.buffer, 0, StateObject.BUFFER_SIZE, 0, new AsyncCallback(ReceiveCallback), state);
                }
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }
        }

        /// <summary>
        /// 回调函数
        /// </summary>
        /// <param name="ar"></param>
        private void ReceiveCallback(IAsyncResult ar)
        {

            StateObject state = (StateObject)ar.AsyncState;
            Socket socket = state.workSocket;
            try
            {
                if (socket != null && socket.Connected)
                {
                    int length = socket.EndReceive(ar);
                    string message = Encoding.GetEncoding("gb2312").GetString(state.buffer, 0, length);
                    if (message != "")
                    {
                        form.dealTransferMsg(message);//处理消息
                        newDateTime = DateTime.Now;
                        socket.BeginReceive(state.buffer, 0, StateObject.BUFFER_SIZE, 0, new AsyncCallback(ReceiveCallback), state);
                    }
                }
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
                closeSocket();
            }
        }
        /// <summary>
        /// 发送图像信息
        /// </summary>
        /// <param name="time"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool SendPicMsg(string time, string path)
        {
            try
            {
                InfoPicMsg infopic = new InfoPicMsg();
                infopic.devId = global.devid;
                infopic.devtype = 2;
                infopic.func = 101;
                infopic.err = "";
                picMsg pic = new picMsg();
                pic.collectTime = time;
                pic.picStr = Tools.GetBase64FromPic(path);
                infopic.message = pic;

                FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                long size = fs.Length;
                fs.Close();
                InfoPicMsg infopiclog = new InfoPicMsg();
                infopiclog.devId = global.devid;
                infopiclog.devtype = 2;
                infopiclog.func = 101;
                infopiclog.err = "";
                picMsg piclog = new picMsg();
                piclog.collectTime = time;
                piclog.picStr = "图像长度：" + size.ToString() + ";BASE64编码长度：" + pic.picStr.Length;
                infopiclog.message = piclog;
                return SendMsg(infopic.ObjectToJson(), infopiclog.ObjectToJson());
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
                return false;
            }

        }
        /// <summary>
        /// 数据发送
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public bool SendMsg(string cmd)
        {
            try
            {
                if (clientSocket != null && clientSocket.Connected)
                {
                    JObject jo = (JObject)JsonConvert.DeserializeObject(cmd);
                    string func = jo["func"].ToString();
                    byte[] sendBytes = Encoding.GetEncoding("gb2312").GetBytes(cmd + "\r\n");
                    int n = clientSocket.Send(sendBytes);
                    if (n != sendBytes.Length)
                    {
                        if (func == "100")
                            DebOutPut.WriteLog(LogType.Normal, LogDetailedType.KeepAliveLog, "传输数据Socket事件_发送失败:" + cmd);
                        else
                            DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "传输数据Socket事件_发送失败:" + cmd);
                        DebOutPut.DebLog("传输数据Socket事件_发送失败:" + cmd);
                        return false;
                    }
                    else
                    {
                        if (func == "100")
                            DebOutPut.WriteLog(LogType.Normal, LogDetailedType.KeepAliveLog, "传输数据Socket事件_发送成功:" + cmd);
                        else
                            DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "传输数据Socket事件_发送成功:" + cmd);
                        DebOutPut.DebLog("传输数据Socket事件_发送成功:" + cmd);
                        return true;
                    }
                }
                else
                {
                    DebOutPut.DebLog("传输数据Socket事件_数据发送，检测到连接异常！");
                    DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "传输数据Socket事件，检测到连接异常！");
                    closeSocket();
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
                closeSocket();
                return false;
            }
        }

        /// <summary>
        /// 发送图片
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public bool SendMsg(string cmd, string log)
        {
            try
            {
                if (clientSocket != null && clientSocket.Connected)
                {
                    JObject jo = (JObject)JsonConvert.DeserializeObject(cmd);
                    string func = jo["func"].ToString();
                    byte[] sendBytes = Encoding.GetEncoding("gb2312").GetBytes(cmd + "\r\n");
                    int n = clientSocket.Send(sendBytes);
                    if (n != sendBytes.Length)
                    {
                        DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "传输数据Socket事件_发送失败:" + log);
                        DebOutPut.DebLog("传输数据Socket事件_发送失败:" + log);
                        return false;
                    }
                    else
                    {
                        DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "传输数据Socket事件_发送成功:" + log);
                        DebOutPut.DebLog("传输数据Socket事件_发送成功:" + log);
                        return true;
                    }
                }
                else
                {
                    DebOutPut.DebLog("传输数据Socket事件_数据发送，检测到连接异常！");
                    DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "传输数据Socket事件，检测到连接异常！");
                    closeSocket();
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
                closeSocket();
                return false;
            }
        }

        /// <summary>
        /// 发送设备参数
        /// </summary>
        /// <param name="hour">时</param>
        /// <param name="time">分</param>
        /// <param name="sampleminute">采集时间</param>
        /// <param name="samplemStrength">采集强度</param>
        /// <param name="peiyangyeCount">培养液量</param>
        /// <param name="fanshilinCount">粘附液量</param>
        /// <param name="peiyangTime">培养时间</param>
        /// <param name="minSteps">最小步数</param>
        /// <param name="maxSteps">最大步数</param>
        /// <param name="clearCount">单点选图</param>
        /// <param name="leftMaxSteps">左侧步数</param>
        /// <param name="rightMaxSteps">右侧步数</param>
        /// <param name="liftRightClearCount">多点选图</param>
        /// <param name="liftRightMoveInterval">移动间隔</param>
        /// <param name="isBug">调试、正常</param>
        public void sendDevParam(int hour, int time, int sampleminute, int samplemStrength, int peiyangyeCount, int fanshilinCount, int peiyangTime, int minSteps, int maxSteps, int clearCount, int leftMaxSteps, int rightMaxSteps, int liftRightClearCount, int liftRightMoveInterval, int fanStrengthMax, int fanStrengthMin, int tranStepsMin, int tranStepsMax, int tranClearCount, int xCorrecting, int yCorrecting, int yJustRange, int yNegaRange, int yInterval, int yJustCom, int yNageCom, int yFirst, int yCheck, int isBug)
        {
            try
            {
                devParam dev = new devParam();
                dev.func = 110;
                dev.err = "";
                dev.devId = global.devid; ;
                param parm = new param();
                parm.collectHour = hour;
                parm.collectTime = time;
                parm.sampleMinutes = sampleminute;
                parm.samplemStrength = samplemStrength;
                parm.cultureCount = peiyangyeCount;
                parm.VaselineCount = fanshilinCount;
                parm.cultureTime = peiyangTime;
                parm.minSteps = minSteps;
                parm.maxSteps = maxSteps;
                parm.clearCount = clearCount;
                parm.leftMaxSteps = leftMaxSteps;
                parm.rightMaxSteps = rightMaxSteps;
                parm.liftRightClearCount = liftRightClearCount;
                parm.liftRightMoveInterval = liftRightMoveInterval;
                parm.fanStrengthMax = fanStrengthMax;
                parm.fanStrengthMin = fanStrengthMin;
                parm.tranStepsMin = tranStepsMin;
                parm.tranStepsMax = tranStepsMax;
                parm.tranClearCount = tranClearCount;
                parm.xCorrecting = xCorrecting;
                parm.yCorrecting = yCorrecting;
                parm.yJustRange = yJustRange;
                parm.yNegaRange = yNegaRange;
                parm.yInterval = yInterval;
                parm.yJustCom = yJustCom;
                parm.yNageCom = yNageCom;
                parm.yFirst = yFirst;
                parm.yCheck = yCheck;
                parm.isBug = isBug;//1是调试，0是在正常
                dev.message = parm;
                SendMsg(dev.ObjectToJson());
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }

        }

        /// <summary>
        /// 获取设备当前异常状态和当前动作
        /// </summary>
        /// <param name="replay">设备编码</param>
        /// <param name="err">错误信息</param>
        /// <param name="status">当前状态</param>
        /// <param name="location">当前位置</param>
        public void senddevStatus(int replay, String err, String status, string location)
        {
            try
            {
                devStatus msg = new devStatus();
                msg.func = replay;
                msg.err = err;
                msg.devId = global.devid;
                msg.message = status;
                msg.location = location;
                SendMsg(msg.ObjectToJson());
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }
        }

        /// <summary>
        /// 发送载玻片数量和当前工作模式
        /// </summary>
        /// <param name="replay">功能码</param>
        /// <param name="err">错误信息</param>
        /// <param name="remain">载玻片数量</param>
        /// <param name="currMode">当前模式</param>
        /// <param name="temp">腔体温度</param>
        public void SendSlideGlassCount(int replay, String err, int remain, int currMode, float temp)
        {
            try
            {
                SlideGlassCount msg = new SlideGlassCount();
                msg.func = replay;
                msg.err = err;
                msg.devId = global.devid;

                SlideGlassMessage message = new SlideGlassMessage();
                message.nums = remain;
                message.workMode = currMode;
                message.temp = temp;
                msg.message = message;
                SendMsg(msg.ObjectToJson());
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }
        }
        public void sendtimeControl(int replay, String err, String timecontrl)
        {
            try
            {
                worktimes msg = new worktimes();
                msg.func = replay;
                msg.err = err;
                msg.devId = global.devid;
                msg.timecontrol = timecontrl;

                SendMsg(msg.ObjectToJson());
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }


        }

        /// <summary>
        /// 发送工作模式
        /// </summary>
        /// <param name="runflag"></param>
        public void sendWorkMode(String runflag)
        {
            try
            {
                WorkModeMsg location = new WorkModeMsg();

                location.devId = global.devid;
                location.func = 136;
                location.err = "";
                location.model = runflag;
                SendMsg(location.ObjectToJson());
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }

        }

        /*回应状态*/
        public void Replay(int replay, string status, string err)
        {
            try
            {
                ReplayMsg msg = new ReplayMsg();
                msg.func = replay;
                msg.err = err;
                msg.devId = global.devid; ;
                msg.message = status;
                string str = msg.ObjectToJson();
                SendMsg(msg.ObjectToJson());
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }

        }
        /// <summary>
        /// 关闭Socket
        /// </summary>
        public void closeSocket()
        {
            try
            {
                if (clientSocket != null && clientSocket.Connected)
                {
                    clientSocket.Shutdown(SocketShutdown.Both);
                    Thread.Sleep(10);
                    clientSocket.Disconnect(false);
                    DebOutPut.DebLog("传输数据设备主动断开连接");
                    DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "传输数据设备主动断开连接");
                }
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }
            finally
            {
                if (clientSocket != null)
                {
                    clientSocket.Close();
                    clientSocket = null;
                }
            }
        }

        /// <summary>
        /// 发送连接保持帧
        /// </summary>
        public void sendKeepLive()
        {
            try
            {
                KeepLive lc = new KeepLive();
                lc.message = "keep-alive";
                lc.devId = global.devid;
                lc.func = 100;
                lc.err = "";
                SendMsg(lc.ObjectToJson());
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }

        }

    }

    /// <summary>
    /// 设备当前状态和当前动作
    /// </summary>
    class devStatus
    {
        public string devId { set; get; }
        public string err { set; get; }
        public int func { set; get; }
        public string message { set; get; }
        public string location { set; get; }

        public string ObjectToJson()
        {
            JavaScriptSerializer jsonSerialize = new JavaScriptSerializer();
            return jsonSerialize.Serialize(this);
        }
    }

    class SlideGlassCount
    {
        public int func { set; get; }
        public string err { set; get; }
        public string devId { set; get; }
        public SlideGlassMessage message { set; get; }
        public string ObjectToJson()
        {
            JavaScriptSerializer jsonSerialize = new JavaScriptSerializer();
            return jsonSerialize.Serialize(this);
        }
    }
    class SlideGlassMessage
    {
        public int nums { set; get; }
        public int workMode { set; get; }
        public float temp { set; get; }
    }

    class worktimes
    {
        public string devId { set; get; }
        public string err { set; get; }
        public int func { set; get; }

        public String timecontrol { set; get; }

        public string ObjectToJson()
        {
            JavaScriptSerializer jsonSerialize = new JavaScriptSerializer();
            return jsonSerialize.Serialize(this);
        }
    }
}
