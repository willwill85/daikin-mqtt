using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Config;
using SuperSocket.Common;
using SuperSocket.SocketBase.Protocol;
using SuperSocket.SocketEngine;

namespace mqtttest
{

    public partial class DaikinDeamon : Form
    {

        String productKey;
        String deviceName;
        String deviceSecret;
        MqttClient mqttClient;
        static Thread serverThrad;
        static AppServer appServer { get; set; }
        public int signal_counter = 0;
        public string buffer = "";
        AppSession mysession;
        //socket 初始化
        public void connect()
        {
            appServer = new AppServer();
            //Setup the appServer
            if (!appServer.Setup(1924)) //Setup with listening port
            {
                Socket_LOG ("Failed to setup!");
                return;
            }

            //Try to start the appServer
            if (!appServer.Start())
            {
                Socket_LOG("Failed to start!");
                return;
            }
            appServer.NewSessionConnected += new SessionHandler<AppSession>(appServer_NewSessionConnected);
            appServer.SessionClosed += appServer_NewSessionClosed;
            appServer.NewRequestReceived += new RequestHandler<AppSession, StringRequestInfo>(appServer_NewRequestReceived);
            
        }
        //连接事件
         void appServer_NewSessionConnected(AppSession session)
        {
            string a = "服务端得到来自客户端的连接成功";
            Socket_LOG(a);
            mysession = session;
            pictureBox1.Image = Properties.Resources.connect;
            //session.Send("Welcome to SuperSocket Telnet Server");
        }
        //断开事件
         void appServer_NewSessionClosed(AppSession session, SuperSocket.SocketBase.CloseReason aaa)
        {
            Socket_LOG("服务端 失去 来自客户端的连接" + session.SessionID + aaa.ToString());
            pictureBox1.Image = Properties.Resources.disconnect;
            var count = appServer.GetAllSessions().Count();
            Console.WriteLine(count);
        }
        //socket收到消息
        void appServer_NewRequestReceived(AppSession session, StringRequestInfo requestInfo)
        {
            push_buffer(requestInfo.Key);
        }
        void push_buffer(string temp)
        {
            buffer += temp;
            for (int i = 0; i < temp.Count(); i++)
            {
                if (temp[i] == '{')
                    signal_counter++;
                if (temp[i] == '}')
                    signal_counter--;
            }
            if (buffer.Length > 20 && signal_counter == 0)
            {
                textBox5.Text += buffer;
                publish_status(buffer);
                buffer = "";
            }
        }

        //socket sc;
        public DaikinDeamon()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
            connect();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            productKey = textBox1.Text;
            deviceName = textBox2.Text;
            deviceSecret = textBox3.Text;

            // MQTT连接参数。
            MqttSign sign = new MqttSign();
            sign.calculate(productKey, deviceName, deviceSecret);

            Console.WriteLine("username: " + sign.getUsername());
            Console.WriteLine("password: " + sign.getPassword());
            Console.WriteLine("clientid: " + sign.getClientid());

            int port = 443;
            String broker = productKey + ".iot-as-mqtt.cn-shanghai.aliyuncs.com";

            mqttClient = new MqttClient(broker, port, true, MqttSslProtocols.TLSv1_2, null, null);
            mqttClient.Connect(sign.getClientid(), sign.getUsername(), sign.getPassword());

            LOG("Broker: " + broker + " Connected");

            String topicReply = "/sys/" + productKey + "/" + deviceName + "/thing/event/property/post_reply";

            mqttClient.MqttMsgPublishReceived += MqttPostProperty_MqttMsgPublishReceived;
            mqttClient.Subscribe(new string[] { topicReply }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });



        }
        //MQTT 收到消息转发到本地
        private void MqttPostProperty_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            LOG("reply topic  :" + e.Topic);
            LOG("reply payload:" + Encoding.UTF8.GetString(e.Message));
            iot_message_set message = JsonConvert.DeserializeObject<iot_message_set>(Encoding.UTF8.GetString(e.Message).Replace("1:", ""));
            socket_set ss = new socket_set();
            ss.DeviceID = deviceName;
            ss.CMD = message.@params;
            Socket_LOG(JsonConvert.SerializeObject(ss));
           
            try
            {
                mysession.Send(JsonConvert.SerializeObject(ss));
                //sc.SendData(JsonConvert.SerializeObject(ss));
            }
            catch {
                Socket_LOG("Socket发送数据失败");
            }
            //Encoding.UTF8.GetString()
        }
        public void LOG(string log)
        {
            textBox4.Text += log + "\r\n";
        }
        public void Socket_LOG(string log)
        {
            textBox5.Text += log + "\r\n";
        }
        //UNIX 时间转化
        public long ConvertDateTimeInt(System.DateTime time)
        {
            //double intResult = 0;
            System.DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1, 0, 0, 0, 0));
            //intResult = (time- startTime).TotalMilliseconds;
            long t = (time.Ticks - startTime.Ticks) / 10000; //除10000调整为13位
            return t;
        }

        //回传设备属性到云
        public void publish_status(string socketdata)
        {
            string topic = "/sys/g5v3zxg641h/" + deviceName + "/thing/event/property/post";
            iot_message_get img = new iot_message_get();
            socket_get sg = JsonConvert.DeserializeObject<socket_get>(socketdata);

            img.id = ConvertDateTimeInt(System.DateTime.Now);
            img.method = "thing.event.property.post";
            img.version = "1.0";
            //Device_status_get dsg = new Device_status_get();
            //img.@params = dsg;
            img.@params = sg.STATUS;
            string msg = JsonConvert.SerializeObject(img);
            msg = JSONtoALIiot(msg);
            byte[] message = Encoding.ASCII.GetBytes(msg);
            mqttClient.Publish(topic, message);
        }
        //阿里云 json 特殊形式
        public static string JSONtoALIiot(string json)
        {
            string[] oldkey = { "Switch", "Mode", "Temperature", "Humility", "Direction", "AirFlow", "Ventilation", "FilterReset", "Exception", "Connection" };
            for (int i = 0; i < oldkey.Count(); i++)
            {
                json = json.Replace(oldkey[i], "1:" + oldkey[i]);

            }
            return json;
        }

        private void DaikinDeamon_FormClosing(object sender, FormClosingEventArgs e)
        {
            mqttClient.Disconnect();
        }
    }
   
}
