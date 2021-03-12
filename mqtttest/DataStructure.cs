using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace mqtttest
{
    class iot_message_set
    {
        public string method { get; set; }
        public string id { get; set; }
        public Object @params { get; set; }
    }
    class socket_set
    {
        public string DeviceID { get; set; }
        public Object CMD { get;set;}
    }
    class iot_message_get
    {
        public Int64 id { get; set; }
        public Device_status_get @params { get; set; }
        public string version { get; set; }
        public string method { get; set; }
    }
    class Device_status_get
    {
        public int Switch { get; set; }            //1 代表 ON 代表开机，0代表 OFF代表关机
        public int Mode { get; set; }                  // 1-N 个模式，代表制冷，制热，送风，预热等（需要配合办公室空调定义）
        public int Temperature { get; set; }        //设定空调的温度
        public int Temperature_R { get; set; }        //设定空调的温度
        public int Humility { get; set; }            // 设定空调的湿度
        public int Humility_R { get; set; }        //设定空调的温度
        public int Direction { get; set; }            // 1-N 个模式，代表 摆动，自动，停止等（需要配合办公室空调定义）
        public int AirFlow { get; set; }          //LL，L，M，H，HH，AUTO 几个风量档位分别对应 0,1,2,3,4,5
        public int Ventilation { get; set; }  // 换气模式，包含 AUTO HEAT NORMAL  分别对应 0,1,2
        public int FilterReset { get; set; }    //过滤信号复位 YES or NO 分别对应 1,0
        public string @Exception = "";     //错误异常代码,依照大金的错误代码来定义
        public int Connection { get; set; }       //OK 或者 FAIL 来表示室内机的通信状态 分别对应 1,0
    }
    class socket_get
    {
        public string DeviceID { get; set; }
        public Device_status_get STATUS { get; set; }
    }
    class CryptoUtil
    {
        public static String hmacSha256(String plainText, String key)
        {
            var encoding = new System.Text.UTF8Encoding();
            byte[] plainTextBytes = encoding.GetBytes(plainText);
            byte[] keyBytes = encoding.GetBytes(key);

            HMACSHA256 hmac = new HMACSHA256(keyBytes);
            byte[] sign = hmac.ComputeHash(plainTextBytes);
            return BitConverter.ToString(sign).Replace("-", string.Empty);
        }
    }
    public class MqttSign
    {
        private String username = "";

        private String password = "";

        private String clientid = "";

        public String getUsername() { return this.username; }

        public String getPassword() { return this.password; }

        public String getClientid() { return this.clientid; }

        public bool calculate(String productKey, String deviceName, String deviceSecret)
        {
            if (productKey == null || deviceName == null || deviceSecret == null)
            {
                return false;
            }

            //MQTT用户名
            this.username = deviceName + "&" + productKey;

            //MQTT密码
            String timestamp = Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds).ToString();
            String plainPasswd = "clientId" + productKey + "." + deviceName + "deviceName" +
                    deviceName + "productKey" + productKey + "timestamp" + timestamp;
            this.password = CryptoUtil.hmacSha256(plainPasswd, deviceSecret);

            //MQTT ClientId
            this.clientid = productKey + "." + deviceName + "|" + "timestamp=" + timestamp +
                    ",_v=paho-c#-1.0.0,securemode=2,signmethod=hmacsha256|";

            return true;
        }
    }

}
