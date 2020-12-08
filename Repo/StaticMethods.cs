using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace SmartCamera.Repo
{
    public class StaticMethods
    {
        public static bool CheckOnlineStatus()
        {
            bool retVal = false;
            try
            {
                Ping pingSender = new Ping();
                PingOptions options = new PingOptions();
                // Use the default Ttl value which is 128,
                // but change the fragmentation behavior.
                options.DontFragment = true;
                // Create a buffer of 32 bytes of data to be transmitted.
                string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                byte[] buffer = Encoding.ASCII.GetBytes(data);
                int timeout = 120;

                PingReply reply = pingSender.Send("www.google.com", timeout, buffer, options);
                if (reply.Status == IPStatus.Success)
                {
                    retVal = true;
                }
            }
            catch (Exception ex)
            {
                retVal = false;
               
            }
            return retVal;
        }
    }
}
