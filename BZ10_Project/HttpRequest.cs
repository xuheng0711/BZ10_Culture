using BZ10.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace BZ10
{
    public class HttpRequest
    {
        public string deviceId = ""; //设备ID
        private string statusServerUrl = "http://bz.fengyuniot.com/api/callback/okqStatusServerCallback";
        private string dataServerUrl = "http://bz.fengyuniot.com/api/callback/okqDataServerCallback";

        /// <summary>
        /// 获取设备当前状态
        /// </summary>
        /// <param name="replay">功能码</param>
        /// <param name="err">错误信息</param>
        /// <param name="status">当前状态</param>
        public void sendDeviceStatus(int replay, String err, String status)
        {
            try
            {
                devStatus msg = new devStatus();
                msg.func = replay;
                msg.err = err;
                msg.devId = deviceId;
                msg.message = status;
                Post(statusServerUrl, msg.ObjectToJson());
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
            }
        }

        /// <summary>
        /// 上传图片数据
        /// </summary>
        /// <param name="time">采集时间</param>
        /// <param name="path">图片路径</param>
        public bool SendPicMsg(string time, string path)
        {
            try
            {
                InfoPicMsg infopic = new InfoPicMsg();
                infopic.devId = deviceId;
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
                infopiclog.devId = deviceId;
                infopiclog.devtype = 2;
                infopiclog.func = 101;
                infopiclog.err = "";
                picMsg piclog = new picMsg();
                piclog.collectTime = time;
                piclog.picStr = "图像长度：" + size.ToString() + ";BASE64编码长度：" + pic.picStr.Length;
                infopiclog.message = piclog;
                DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, infopiclog.ObjectToJson());
                string res = Post(dataServerUrl, infopic.ObjectToJson());
                //保存日志
                DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "发送返回结果：" + res);
                Result result = JsonConvert.DeserializeObject<Result>(res);
                if (result.success == 0 && result.devId == infopic.devId && result.func == infopic.func)
                {
                    string sql = "update Record Set Flag = '1' where CollectTime='" + result.collectTime + "'";
                    int x = DB.updateDatabase(sql);
                    if (x < 0)
                    {
                        DebOutPut.DebLog("收到采集信息上传回复，但数据库标志位更改失败！");
                        DebOutPut.WriteLog(LogType.Normal, LogDetailedType.Ordinary, "收到采集信息上传回复，但数据库标志位更改失败！");
                        return false;
                    }
                }
                else
                {
                    return false;
                }
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
        /// Post请求数据
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <returns></returns>
        public string Post(string url, string postData)
        {
            try
            {
                HttpWebRequest webrequest = (HttpWebRequest)HttpWebRequest.Create(url);
                webrequest.Method = "POST";
                webrequest.ContentType = "application/json;charset=utf-8";
                byte[] postdatabyte = Encoding.UTF8.GetBytes(postData);
                webrequest.ContentLength = postdatabyte.Length;
                Stream stream;
                stream = webrequest.GetRequestStream();
                stream.Write(postdatabyte, 0, postdatabyte.Length);
                stream.Close();
                using (var httpWebResponse = webrequest.GetResponse())
                {
                    using (StreamReader responseStream = new StreamReader(httpWebResponse.GetResponseStream()))
                    {
                        String ret = responseStream.ReadToEnd();
                        string result = ret.ToString();
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                DebOutPut.DebLog(ex.ToString());
                DebOutPut.WriteLog(LogType.Error, LogDetailedType.Ordinary, ex.ToString());
                return "";
            }
        }


        class SlideGlassCount
        {
            public int func { set; get; }
            public string err { set; get; }
            public string devId { set; get; }
            public Message message { set; get; }
            public string ObjectToJson()
            {
                JavaScriptSerializer jsonSerialize = new JavaScriptSerializer();
                return jsonSerialize.Serialize(this);
            }
        }
        class Message
        {
            public int nums { set; get; }
            public int workMode { set; get; }
            public float temp { set; get; }
        }

        class CurrActive
        {
            public int func { set; get; }
            public string err { set; get; }
            public string devId { set; get; }
            public string message { set; get; }//0.原点 1.推片 2.滴加粘附液 3.收集 4.滴加培养液 5.回收 6.复位
            public string ObjectToJson()
            {
                JavaScriptSerializer jsonSerialize = new JavaScriptSerializer();
                return jsonSerialize.Serialize(this);
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
            //public string location { set; get; }

            public string ObjectToJson()
            {
                JavaScriptSerializer jsonSerialize = new JavaScriptSerializer();
                return jsonSerialize.Serialize(this);
            }
        }


        public class timespan
        {
            public string time1 { set; get; }
            public string time2 { set; get; }
            public string time3 { set; get; }
            public string time4 { set; get; }
            public string time5 { set; get; }

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

        public class Result
        {
            /// <summary>
            /// 0成功 -1失败
            /// </summary>
            public int success { get; set; }
            public int func { get; set; }
            public string devId { set; get; }
            public string message { set; get; }
            public string collectTime { get; set; }
        }

    }



}
