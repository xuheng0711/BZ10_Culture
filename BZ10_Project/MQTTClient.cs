using Aliyun.OSS;
using BZ10.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace BZ10
{
    public class MQTTClient
    {

        private static MainForm form;//主界面对话框
        MQTTModel mQTTModel = null;//MQTT对象
        /// <summary>
        /// 订阅主题
        /// </summary>
        private string strSubscribeTopic = "";

        /// <summary>
        /// 发布主题
        /// </summary>
        private string strPublishTopic = "";
        /// <summary>
        /// MQTT
        /// </summary>
        public MqttClient client;
        /// <summary>
        /// 程序锁
        /// </summary>
        private readonly object locker = new object();

        /// <summary>
        /// 定时发送心跳
        /// </summary>
        private System.Timers.Timer myTimer = null;
        private int inteval = 30000; //心跳间隔
        public MQTTClient(MainForm form1, MQTTModel param)
        {
            form = (MainForm)form1;
            //MQTT对象
            mQTTModel = param;
            //订阅主题
            strSubscribeTopic = string.Format("downrive/spore/json/{0}", Param.DeviceID);
            //发布主题
            strPublishTopic = string.Format("upstream/spore/json/{0}", Param.DeviceID);
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
                    myTimer.Elapsed += new System.Timers.ElapsedEventHandler(SendKeepLive);
                    myTimer.Interval = inteval;
                }
            }
            catch (Exception ex)
            {
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }
        }

        /// <summary>
        /// 定时发送心跳帧
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void SendKeepLive(object source, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (client != null && client.IsConnected)
                {
                    KeepLive lc = new KeepLive();
                    lc.message = "keep-alive";
                    lc.devId = Param.DeviceID;
                    lc.func = 100;
                    lc.err = "";
                    publishMessage(lc.ObjectToJson());
                }
            }
            catch (Exception ex)
            {
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }
        }

        /// <summary>
        /// 实例化MQTT客户端
        /// </summary>
        public void BuildMqttClient()
        {
            try
            {
                if (string.IsNullOrEmpty(Param.UploadIP) || string.IsNullOrEmpty(Param.UploadPort))
                {
                    DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "未设置MQTT服务器地址或端口号信息");
                    return;
                }
                client = new MqttClient(Param.UploadIP, int.Parse(Param.UploadPort), false, MqttSslProtocols.TLSv1_2, null, null);
                client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;//接收消息
                client.ConnectionClosed += Client_ConnectionClosed;//服务器主动断开重新连接
            }
            catch (Exception ex)
            {
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, string.Format("实例化MQTT客户端异常：{0}", ex.ToString()));
            }
        }

        /// <summary>
        /// MQTT接收消息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            try
            {
                //处理接收到的消息  
                string strReceiveMessage = Encoding.Default.GetString(e.Message);
                if (form.InvokeRequired)
                {
                    MqttClient.MqttMsgPublishEventHandler setpos = new MqttClient.MqttMsgPublishEventHandler(Client_MqttMsgPublishReceived);
                    form.Invoke(setpos, new object[] { sender }, e);
                }
                else
                {
                    DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, string.Format("主题【{0}】，接收消息：{1}", e.Topic, strReceiveMessage));
                    if (string.IsNullOrEmpty(strReceiveMessage))
                    {
                        return;
                    }
                    MQTTMessage modelMessage = JsonConvert.DeserializeObject<MQTTMessage>(strReceiveMessage);
                    if (modelMessage.devId != Param.DeviceID)
                    {
                        return;
                    }
                    form.dealMQTTMsg(strReceiveMessage);
                }
            }
            catch (Exception ex)
            {
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, string.Format("接收异常消息：{0}", ex.ToString()));
            }

        }

        /// <summary>
        /// Mqtt连接断开
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Client_ConnectionClosed(object sender, EventArgs e)
        {
            DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "mqtt连接：" + client.IsConnected);
            if (!client.IsConnected)
            {
                TryContinueConnect(); // 尝试重连
            }
        }

        /// <summary>
        ///  自动重连主体
        /// </summary>
        private void TryContinueConnect()
        {
            if (client.IsConnected)
                return;
            Thread retryThread = new Thread(new ThreadStart(delegate
            {
                while (client == null || !client.IsConnected)
                {
                    if (client.IsConnected)
                        break;
                    if (client == null)
                    {
                        BuildMqttClient();
                        MqttConnect();
                        Thread.Sleep(3000);
                        continue;
                    }
                    try
                    {
                        MqttConnect();
                    }
                    catch (Exception ex)
                    {
                        DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, "重新连接异常:" + ex.ToString());
                    }
                    // 如果还没连接不符合结束条件则睡2秒
                    if (!client.IsConnected)
                        Thread.Sleep(2000);
                }
            }));
            retryThread.Start();
        }


        /// <summary>
        /// 发起一次连接，连接成功则订阅相关主题 
        /// </summary>
        public void MqttConnect()
        {
            try
            {
                if (client == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(Param.MQTTAccount) || string.IsNullOrEmpty(Param.MQTTPassword))
                {
                    DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "未设置MQTT服务器ClientID、账号、密码信息");
                    return;
                }
                string strClientID = Param.MQTTClientID;
                if (string.IsNullOrEmpty(Param.MQTTClientID))
                {
                    strClientID = Guid.NewGuid().ToString();
                }

                client.Connect(strClientID,
                    Param.MQTTAccount,
                    Param.MQTTPassword,
                    true, // 清理会话，默认为true。设置为false可以接收到QoS 1和QoS 2级别的离线消息
                    50 // 客户端向【服务端】发送心跳的时间间隔，默认50秒，设置成0代表不启用心跳，注意不是向服务器的两个连接方
                    );

                DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "mqtt连接：" + client.IsConnected);
                if (client.IsConnected)
                {
                    client.Subscribe(new string[] { strSubscribeTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
                    DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, string.Format("订阅主题{0}成功", strSubscribeTopic));
                }
            }
            catch (Exception ex)
            {
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, "MqttConnect连接异常:" + ex.ToString());
            }
            finally
            {
                //开启定时器，定时发送心跳帧
                if (myTimer == null)
                {
                    InitTimer();
                    myTimer.Enabled = true;
                    DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "心跳帧启动！");
                }
                else if (myTimer.Enabled == false)
                {
                    myTimer.Enabled = true;
                    DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "心跳帧启动！");
                }
            }

        }


        #region 发送信息方法
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
        public void sendDevParam(int hour, int time, int sampleminute, int samplemStrength, int peiyangyeCount, int fanshilinCount, int peiyangTime, int minSteps, int maxSteps, int clearCount, int leftMaxSteps, int rightMaxSteps, int liftRightClearCount, int liftRightMoveInterval, int fanStrengthMax, int fanStrengthMin, int tranStepsMin, int tranStepsMax, int tranClearCount, int xCorrecting, int yCorrecting, int yJustRange, int yNegaRange, int yInterval, int yJustCom, int yNageCom, int yFirst, int yCheck,decimal cultureTemperature,int thermostaticCultureTime, int isBug)
        {
            try
            {
                devParam dev = new devParam();
                dev.func = 110;
                dev.err = "";
                dev.devId = Param.DeviceID;
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
                parm.cultureTemperature = cultureTemperature;
                parm.thermostaticCultureTime = thermostaticCultureTime;
                parm.isBug = isBug;//1是调试，0是在正常
                dev.message = parm;
                publishMessage(dev.ObjectToJson());
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
                msg.devId = Param.DeviceID;
                msg.message = status;
                msg.location = location;
                publishMessage(msg.ObjectToJson());
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
                MQTTSlideGlassCount msg = new MQTTSlideGlassCount();
                msg.func = replay;
                msg.err = err;
                msg.devId = Param.DeviceID;

                CommonMessage message = new CommonMessage();
                message.nums = remain;
                message.workMode = currMode;
                message.temp = temp;
                msg.message = message;
                publishMessage(msg.ObjectToJson());
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }
        }

        /// <summary>
        /// 发送设备时间段
        /// </summary>
        /// <param name="replay"></param>
        /// <param name="err"></param>
        /// <param name="timecontrl"></param>
        public void sendtimeControl(int replay, String err, List<object> timecontrl)
        {
            try
            {
                worktimes msg = new worktimes();
                msg.func = replay;
                msg.err = err;
                msg.devId = Param.DeviceID;
                msg.timecontrol = timecontrl;

                publishMessage(msg.ObjectToJson());
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

                location.devId = Param.DeviceID;
                location.func = 136;
                location.err = "";
                location.model = runflag;
                publishMessage(location.ObjectToJson());
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }

        }

        /// <summary>
        /// 发送位置信息
        /// </summary>
        /// <param name="lat"></param>
        /// <param name="lon"></param>
        public void sendLocation(double lat, double lon)
        {
            try
            {
                LocationMsg location = new LocationMsg();
                location.devId = Param.DeviceID;
                location.func = 102;
                location.err = "";
                location.message.lat = lat;
                location.message.lon = lon;
                publishMessage(location.ObjectToJson());
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }
        }
        /// <summary>
        /// 回应状态
        /// </summary>
        /// <param name="replay"></param>
        /// <param name="status"></param>
        /// <param name="err"></param>
        public void Replay(int replay, string status, string err)
        {
            try
            {
                ReplayMsg msg = new ReplayMsg();
                msg.func = replay;
                msg.err = err;
                msg.devId = Param.DeviceID;
                msg.message = status;
                string str = msg.ObjectToJson();
                publishMessage(msg.ObjectToJson());
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }

        }

        /// <summary>
        /// 发送当前动作
        /// </summary>
        /// <param name="func"></param>
        /// <param name="err"></param>
        /// <param name="active"></param>
        public void SendCurrAction(int func, string err, string active,string state)
        {
            CurrActive currActive = new CurrActive();
            currActive.func = func;
            currActive.err = err;
            currActive.devId = Param.DeviceID;
            currActive.message = active;
            currActive.state = state;
            publishMessage(currActive.ObjectToJson());
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
                string picAliOssUrl = uploadImageAliOSS(path);
                if (string.IsNullOrEmpty(picAliOssUrl))
                {
                    return false;
                }
                string strDeviceID = Param.DeviceID;

                InfoPicMsg infopic = new InfoPicMsg();
                infopic.devId = strDeviceID;
                infopic.devtype = 2;
                infopic.func = 101;
                infopic.err = "";
                picMsg pic = new picMsg();
                pic.collectTime = time;
                pic.picStr = picAliOssUrl;//阿里云Oss图像地址
                infopic.message = pic;
                publishMessage(infopic.ObjectToJson());
                return true;
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
                return false;
            }

        }

        /// <summary>
        /// 上传图片至阿里云OSS服务器
        /// </summary>
        /// <param name="picPath"></param>
        /// <returns></returns>
        private string uploadImageAliOSS(string picPath)
        {
            string picAliOssUrl = "";
            string fileName = System.IO.Path.GetFileName(picPath);
            //填写Object完整路径，完整路径中不能包含Bucket名称
            var objectName = string.Format("spore/{0}", fileName);
            // 创建OssClient实例。

            if (!string.IsNullOrEmpty(Param.OssEndPoint) && !string.IsNullOrEmpty(Param.OssAccessKeyId) && !string.IsNullOrEmpty(Param.OssAccessKeySecret) && !string.IsNullOrEmpty(Param.OssBucketName) && !string.IsNullOrEmpty(Param.OSS_Url))
            {
                var client = new OssClient(Param.OssEndPoint, Param.OssAccessKeyId, Param.OssAccessKeySecret);
                client.PutObject(Param.OssBucketName, objectName, picPath);

                picAliOssUrl = Param.OSS_Url + objectName;
            }
            else
            {
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, "阿里云OSS配置信息不完整");
            }
            return picAliOssUrl;
        }

        #endregion

        /// <summary>
        /// 发布消息
        /// </summary>
        /// <param name="message"></param>
        public void publishMessage(string message, bool isHeartbeat = false)
        {
            lock (locker)
            {
                if (client.IsConnected)
                {
                    byte[] bMessage = Encoding.UTF8.GetBytes(message);
                    client.Publish(strPublishTopic, bMessage, 2, false);
                    DebOutPut.WriteLog(LogType.Normal, isHeartbeat == true ? LogDetailedType.KeepAliveLog : LogDetailedType.Ordinary, string.Format("主题【{0}】，发布消息：{1}", strPublishTopic, message));
                }
            }
        }



    }

    public class MQTTModel
    {
        public string Address { get; set; }
        public string Port { get; set; }
        public string ClientID { get; set; }
        public string Account { get; set; }
        public string Password { get; set; }
    }

    public class MQTTMessage
    {
        /// <summary>
        /// 功能码
        /// </summary>
        public int func { get; set; }
        /// <summary>
        /// 错误信息
        /// </summary>
        public string err { get; set; }
        /// <summary>
        /// 设备号
        /// </summary>
        public string devId { get; set; }
        /// <summary>
        /// 信息
        /// </summary>
        public object message { get; set; }

    }
    class MQTTSlideGlassCount
    {
        public int func { set; get; }
        public string err { set; get; }
        public string devId { set; get; }
        public CommonMessage message { set; get; }
        public string ObjectToJson()
        {
            JavaScriptSerializer jsonSerialize = new JavaScriptSerializer();
            return jsonSerialize.Serialize(this);
        }
    }
    class CommonMessage
    {
        public int nums { set; get; }
        public int workMode { set; get; }
        public float temp { set; get; }
    }

}
