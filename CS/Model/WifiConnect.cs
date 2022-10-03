using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.WiFi;
using Windows.Security.Credentials;

namespace WiFiConnect.Model
{
    public class WifiConnect
    {
        public WiFiConnectionResult ConnectionResult { get; set; }
        public PasswordCredential Password { get; set; }
    }
}
