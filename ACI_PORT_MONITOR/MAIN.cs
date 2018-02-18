using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Configuration;
using System.Collections.Specialized;

using RestSharp;
using Newtonsoft;
using Newtonsoft.Json.Linq;
using SnmpSharpNet;

using System.Runtime.Remoting.Channels.Tcp;
using System.Threading;

namespace ACI_PORT_MONITOR
{
    public partial class MAIN : Form
    {
        string apicIP  = ConfigurationManager.AppSettings.Get("APIC-1_IP");
        string apic_id = ConfigurationManager.AppSettings.Get("APIC_ID");
        string apic_pw = ConfigurationManager.AppSettings.Get("APIC_PASS");
        string line_token = ConfigurationManager.AppSettings.Get("Line_token");
        int MAX_RETRY  = int.Parse(ConfigurationManager.AppSettings.Get("MAX_RETRY"));

        int MIN_GREEN_SCORE = 90;
        int MIN_YELLOW_SCORE = 75;

        int POD1_Retry = 0, POD1_value = -1;

        int Spine_101_Retry = 0, Spine_101_value = -1;
        int Spine_102_Retry = 0, Spine_102_value = -1;
        int Spine_301_Retry = 0, Spine_301_value = -1;
        int Spine_302_Retry = 0, Spine_302_value = -1;
        int Spine_501_Retry = 0, Spine_501_value = -1;
        int Spine_502_Retry = 0, Spine_502_value = -1;

        int DOM1_201_Retry = 0, DOM1_201_value = -1;
        int DOM1_202_Retry = 0, DOM1_202_value = -1;
        int DOM1_203_Retry = 0, DOM1_203_value = -1;
        int DOM1_204_Retry = 0, DOM1_204_value = -1;
        int DOM1_401_Retry = 0, DOM1_401_value = -1;
        int DOM1_402_Retry = 0, DOM1_402_value = -1;
        int DOM1_403_Retry = 0, DOM1_403_value = -1;
        int DOM1_404_Retry = 0, DOM1_404_value = -1;

        int DOM2_211_Retry = 0, DOM2_211_value = -1;
        int DOM2_212_Retry = 0, DOM2_212_value = -1;
        int DOM2_411_Retry = 0, DOM2_411_value = -1;
        int DOM2_412_Retry = 0, DOM2_412_value = -1;
        int DOM2_611_Retry = 0, DOM2_611_value = -1;
        int DOM2_612_Retry = 0, DOM2_612_value = -1;

        int DOM3_221_Retry = 0, DOM3_221_value = -1;
        int DOM3_222_Retry = 0, DOM3_222_value = -1;
        int DOM3_421_Retry = 0, DOM3_421_value = -1;
        int DOM3_422_Retry = 0, DOM3_422_value = -1;
        int DOM3_621_Retry = 0, DOM3_621_value = -1;
        int DOM3_622_Retry = 0, DOM3_622_value = -1;

        int Login_Retry = 0;

        RestClient client = new RestClient();

        public MAIN()
        {
            InitializeComponent();
            client.Timeout = 300;
            //TcpChannel channel = new TcpChannel(10002);
        }

        private Boolean Login_API(string username, string password)
        {
            string sessionId = "", payload = "";
            RestRequest login_post;
            IRestResponse login_post_response;
            JObject login_data;

            client.BaseUrl = new System.Uri("https://" + apicIP + "/api/aaaLogin.json");
            client.CookieContainer = new System.Net.CookieContainer();
            ServicePointManager.ServerCertificateValidationCallback += (RestClient, certificate, chain, sslPolicyErrors) => true;

            payload = "payload{\"aaaUser\":{\"attributes\":{\"name\":\"" + username + "\", \"pwd\":\"" + password + "\"}}}";

            login_post = new RestRequest(Method.POST);
            login_post.AddHeader("content-type", "application/json");
            login_post.AddParameter("application/json", payload, ParameterType.RequestBody);

            try
            {
                login_post_response = client.Execute(login_post);
                login_data = JObject.Parse(login_post_response.Content);
                sessionId = (login_data["imdata"][0]["aaaLogin"]["attributes"]["sessionId"].ToString());

                Timer_SPINE_BGW.Enabled = true;
                Timer_DOM1_BGW.Enabled = true;
                Timer_DOM2_BGW.Enabled = true;
                Timer_DOM3_BGW.Enabled = true;

                AAA_Refresh.Enabled = true;

                Status_bar.Text = "sessionId = " + sessionId;
            }
            catch
            {
                //Status_bar.Text = "Can't connect to " + apicIP;
                //LineNotify("Can't connect to " + apicIP);
                ///MessageBox.Show("Can't connect to " + apicIP, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true; // no error
        }


        private int LineNotify(string msg)
        {
            RestRequest line_post;
            RestClient line_client = new RestClient
            {
                BaseUrl = new System.Uri("https://notify-api.line.me/api/notify")
            };
            ServicePointManager.ServerCertificateValidationCallback += (RestClient, certificate, chain, sslPolicyErrors) => true;

            line_post = new RestRequest(Method.POST); ;
            line_post.AddHeader("Authorization", string.Format("Bearer " + line_token));
            line_post.AddHeader("content-type", "application/x-www-form-urlencoded");
            line_post.AddParameter("message", msg);

            try
            {
                IRestResponse response = line_client.Execute(line_post);
                return int.Parse((JObject.Parse(response.Content)["status"].ToString()));
            }
            catch
            {
                Status_bar.Text = "Can't connect to line server !!";
                MessageBox.Show("Can't connect to line server !!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private void LoginToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Login_Retry = 0;
            while (Login_API(apic_id, apic_pw) == false && Login_Retry <= MAX_RETRY)
            {
                Login_Retry++;
                Status_bar.Text = "Can't connect to " + apicIP + " #" + Login_Retry;
            }
            if (Login_Retry > MAX_RETRY)
            {
                LineNotify("Can't connect to " + apicIP);
            }
            else
            {
                Login_Retry = 0;
            }
        }

        private int Health_Check_API(string NODE)
        {
            int health_filed;
            RestRequest request = new RestRequest();
            client.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + NODE + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client.Execute(request);

            try
            {
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["cur"].ToString()));
                return health_filed;
            }
            catch
            {
                return -1;
            }
        }

        private void AAA_Refresh_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            while (Login_API(apic_id, apic_pw) == false && Login_Retry <= MAX_RETRY)
            {
                Login_Retry++;
                Status_bar.Text = "Can't connect to " + apicIP + " #" + Login_Retry;
            }
            if (Login_Retry > MAX_RETRY)
            {
                LineNotify("Can't connect to " + apicIP);
            }
            else
            {
                Login_Retry = 0;
            }
        }

        private void TESTLineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LineNotify("==TEST==");
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void SetingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Setting_Form Setting_Form = new Setting_Form();
            Setting_Form.ShowDialog();
        }

        //====================================== BGW POD1 ======================================

        private void BGW_POD1_DoWork(object sender, DoWorkEventArgs e)
        {
            int health_filed;
            RestRequest request = new RestRequest();
            client.BaseUrl = new System.Uri("https://" + apicIP + "//api/node/mo/topology/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client.Execute(request);
            try
            {
                POD1_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["fabricHealthTotal"]["attributes"]["cur"].ToString()));
                if (POD1_value != health_filed && POD1_value != -1)
                {
                    LineNotify("POD1 score " + POD1_value + " -> " + health_filed);
                    POD1_value = health_filed;
                    Health_POD1_score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_POD1_score.Text = health_filed.ToString();
                    }));
                }
                if (POD1_value == -1)
                {
                    POD1_value = health_filed;
                    Health_POD1_score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_POD1_score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                POD1_Retry++;
                Status_bar.Text = "Cann't get health POD1 #" + POD1_Retry;
                if (POD1_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health POD1");
                }
                return;
            }
        }

        //====================================== BGW SPINE ======================================

        private void BGW_Spine_101_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(50);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "101" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                Spine_101_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["cur"].ToString()));
                if (health_filed != Spine_101_value && Spine_101_value != -1)
                {
                    LineNotify("Spine 101 score " + Spine_101_value + " -> " + health_filed);
                    Spine_101_value = health_filed;
                    Health_101_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_101_Score.Text = health_filed.ToString();
                    }));
                }
                if (Spine_101_value == -1)
                {
                    Spine_101_value = health_filed;
                    Health_101_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_101_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                Spine_101_Retry++;
                Status_bar.Text = "Cann't get health NODE101 #" + Spine_101_Retry;
                if (Spine_101_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE101");
                }
                return;
            }
        }

        private void BGW_Spine_102_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(100);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "102" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                Spine_102_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != Spine_102_value && Spine_102_value != -1)
                {
                    LineNotify("Spine 102 score " + Spine_102_value + " -> " + health_filed);
                    Spine_102_value = health_filed;
                    Health_102_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_102_Score.Text = health_filed.ToString();
                    }));
                }
                if (Spine_102_value == -1)
                {
                    Spine_102_value = health_filed;
                    Health_102_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_102_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                Spine_102_Retry++;
                Status_bar.Text = "Cann't get health NODE102 #" + Spine_102_Retry;
                if (Spine_102_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE102");
                }
                return;
            }
        }

        private void BGW_Spine_301_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(150);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "301" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                Spine_301_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != Spine_301_value && Spine_301_value != -1)
                {
                    LineNotify("Spine 301 score " + Spine_301_value + " -> " + health_filed);
                    Spine_301_value = health_filed;
                    Health_301_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_301_Score.Text = health_filed.ToString();
                    }));
                }
                if (Spine_301_value == -1)
                {
                    Spine_301_value = health_filed;
                    Health_301_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_301_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                Spine_301_Retry++;
                Status_bar.Text = "Cann't get health NODE301 #" + Spine_301_Retry;
                if (Spine_301_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE301");
                }
                return;
            }
        }

        private void BGW_Spine_302_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(200);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "302" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                Spine_302_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != Spine_302_value && Spine_302_value != -1)
                {
                    LineNotify("Spine 302 score " + Spine_302_value + " -> " + health_filed);
                    Spine_302_value = health_filed;
                    Health_302_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_302_Score.Text = health_filed.ToString();
                    }));
                }
                if (Spine_302_value == -1)
                {
                    Spine_302_value = health_filed;
                    Health_302_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_302_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                Spine_302_Retry++;
                Status_bar.Text = "Cann't get health NODE302 #" + Spine_302_Retry;
                if (Spine_302_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE302");
                }
                return;
            }
        }

        private void BGW_Spine_501_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(250);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "501" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                Spine_501_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != Spine_501_value && Spine_501_value != -1)
                {
                    LineNotify("Spine 501 score " + Spine_501_value + " -> " + health_filed);
                    Spine_501_value = health_filed;
                    Health_501_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_501_Score.Text = health_filed.ToString();
                    }));
                }
                if (Spine_501_value == -1)
                {
                    Spine_501_value = health_filed;
                    Health_501_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_501_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                Spine_501_Retry++;
                Status_bar.Text = "Cann't get health NODE501 #" + Spine_501_Retry;
                if (Spine_501_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE501");
                }
                return;
            }
        }

        private void BGW_Spine_502_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(300);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "502" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                Spine_502_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != Spine_502_value && Spine_502_value != -1)
                {
                    LineNotify("Spine 502 score " + Spine_502_value + " -> " + health_filed);
                    Spine_502_value = health_filed;
                    Health_502_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_502_Score.Text = health_filed.ToString();
                    }));
                }
                if (Spine_502_value == -1)
                {
                    Spine_502_value = health_filed;
                    Health_502_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_502_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                Spine_502_Retry++;
                Status_bar.Text = "Cann't get health NODE502 #" + Spine_502_Retry;
                if (Spine_502_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE502");
                }
                return;
            }
        }

        //====================================== BGW DOM1 ======================================

        private void BGW_Node_201_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(50);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "201" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM1_201_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM1_201_value && DOM1_201_value != -1)
                {
                    LineNotify("Node201 score " + DOM1_201_value + " -> " + health_filed);
                    DOM1_201_value = health_filed;
                    Health_201_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_201_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM1_201_value == -1)
                {
                    DOM1_201_value = health_filed;
                    Health_201_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_201_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM1_201_Retry++;
                Status_bar.Text = "Cann't get health NODE201 #" + DOM1_201_Retry;
                if (DOM1_201_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE201");
                }
                return;
            }
        }

        private void BGW_Node_202_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(100);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "202" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM1_202_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM1_202_value && DOM1_202_value != -1)
                {
                    LineNotify("Node202 score " + DOM1_202_value + " -> " + health_filed);
                    DOM1_202_value = health_filed;
                    Health_202_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_202_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM1_202_value == -1)
                {
                    DOM1_202_value = health_filed;
                    Health_202_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_202_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM1_202_Retry++;
                Status_bar.Text = "Cann't get health NODE202 #" + DOM1_202_Retry;
                if (DOM1_202_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE202");
                }
                return;
            }
        }

        private void BGW_Node_203_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(150);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "203" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM1_203_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM1_203_value && DOM1_203_value != -1)
                {
                    LineNotify("Node203 score " + DOM1_203_value + " -> " + health_filed);
                    DOM1_203_value = health_filed;
                    Health_203_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_203_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM1_203_value == -1)
                {
                    DOM1_203_value = health_filed;
                    Health_203_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_203_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM1_203_Retry++;
                Status_bar.Text = "Cann't get health NODE203 #" + DOM1_203_Retry;
                if (DOM1_203_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE203");
                }
                return;
            }
        }

        private void BGW_Node_204_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(200);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "204" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM1_204_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM1_204_value && DOM1_204_value != -1)
                {
                    LineNotify("Node204 score " + DOM1_204_value + " -> " + health_filed);
                    DOM1_204_value = health_filed;
                    Health_204_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_204_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM1_204_value == -1)
                {
                    DOM1_204_value = health_filed;
                    Health_204_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_204_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM1_204_Retry++;
                Status_bar.Text = "Cann't get health NODE204 #" + DOM1_204_Retry;
                if (DOM1_204_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE204");
                }
                return;
            }
        }

        private void BGW_Node_401_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(250);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "401" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM1_401_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM1_401_value && DOM1_401_value != -1)
                {
                    LineNotify("Node401 score " + DOM1_401_value + " -> " + health_filed);
                    DOM1_401_value = health_filed;
                    Health_401_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_401_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM1_401_value == -1)
                {
                    DOM1_401_value = health_filed;
                    Health_401_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_401_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM1_401_Retry++;
                Status_bar.Text = "Cann't get health NODE401 #" + DOM1_401_Retry;
                if (DOM1_401_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE401");
                }
                return;
            }
        }

        private void BGW_Node_402_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(300);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "402" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM1_402_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM1_402_value && DOM1_402_value != -1)
                {
                    LineNotify("Node402 score " + DOM1_402_value + " -> " + health_filed);
                    DOM1_402_value = health_filed;
                    Health_402_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_402_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM1_402_value == -1)
                {
                    DOM1_402_value = health_filed;
                    Health_402_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_402_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM1_402_Retry++;
                Status_bar.Text = "Cann't get health NODE402 #" + DOM1_402_Retry;
                if (DOM1_402_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE402");
                }
                return;
            }
        }

        private void BGW_Node_403_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(350);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "403" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM1_403_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM1_403_value && DOM1_403_value != -1)
                {
                    LineNotify("Node403 score " + DOM1_403_value + " -> " + health_filed);
                    DOM1_403_value = health_filed;
                    Health_403_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_403_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM1_403_value == -1)
                {
                    DOM1_403_value = health_filed;
                    Health_403_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_403_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM1_403_Retry++;
                Status_bar.Text = "Cann't get health NODE403 #" + DOM1_403_Retry;
                if (DOM1_403_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE403");
                }
                return;
            }
        }

        private void BGW_Node_404_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(400);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "404" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM1_404_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM1_404_value && DOM1_404_value != -1)
                {
                    LineNotify("Node404 score " + DOM1_404_value + " -> " + health_filed);
                    DOM1_404_value = health_filed;
                    Health_404_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_404_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM1_404_value == -1)
                {
                    DOM1_404_value = health_filed;
                    Health_404_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_404_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM1_404_Retry++;
                Status_bar.Text = "Cann't get health NODE404 #" + DOM1_404_Retry;
                if (DOM1_404_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE404");
                }
                return;
            }
        }

        //====================================== BGW DOM2 ======================================

        private void BGW_Node_211_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(50);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "211" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM2_211_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM2_211_value && DOM2_211_value != -1)
                {
                    LineNotify("Node211 score " + DOM2_211_value + " -> " + health_filed);
                    DOM2_211_value = health_filed;
                    Health_211_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_211_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM2_211_value == -1)
                {
                    DOM2_211_value = health_filed;
                    Health_211_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_211_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM2_211_Retry++;
                Status_bar.Text = "Cann't get health NODE211 #" + DOM2_211_Retry;
                if (DOM2_211_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE211");
                }
                return;
            }
        }

        private void BGW_Node_212_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(100);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "212" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM2_212_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM2_212_value && DOM2_212_value != -1)
                {
                    LineNotify("Node212 score " + DOM2_212_value + " -> " + health_filed);
                    DOM2_212_value = health_filed;
                    Health_212_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_212_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM2_212_value == -1)
                {
                    DOM2_212_value = health_filed;
                    Health_212_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_212_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM2_212_Retry++;
                Status_bar.Text = "Cann't get health NODE212 #" + DOM2_212_Retry;
                if (DOM2_212_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE212");
                }
                return;
            }
        }

        private void BGW_Node_411_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(150);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "411" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM2_411_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM2_411_value && DOM2_411_value != -1)
                {
                    LineNotify("Node411 score " + DOM2_411_value + " -> " + health_filed);
                    DOM2_411_value = health_filed;
                    Health_411_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_411_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM2_411_value == -1)
                {
                    DOM2_411_value = health_filed;
                    Health_411_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_411_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM2_411_Retry++;
                Status_bar.Text = "Cann't get health NODE411 #" + DOM2_411_Retry;
                if (DOM2_411_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE411");
                }
                return;
            }
        }

        private void BGW_Node_412_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(200);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "412" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM2_412_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM2_412_value && DOM2_412_value != -1)
                {
                    LineNotify("Node412 score " + DOM2_412_value + " -> " + health_filed);
                    DOM2_412_value = health_filed;
                    Health_412_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_412_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM2_412_value == -1)
                {
                    DOM2_412_value = health_filed;
                    Health_412_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_412_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM2_412_Retry++;
                Status_bar.Text = "Cann't get health NODE412 #" + DOM2_412_Retry;
                if (DOM2_412_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE412");
                }
                return;
            }
        }

        private void BGW_Node_611_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(250);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "611" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM2_611_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM2_611_value && DOM2_611_value != -1)
                {
                    LineNotify("Node611 score " + DOM2_611_value + " -> " + health_filed);
                    DOM2_611_value = health_filed;
                    Health_611_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_611_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM2_611_value == -1)
                {
                    DOM2_611_value = health_filed;
                    Health_611_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_611_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM2_611_Retry++;
                Status_bar.Text = "Cann't get health NODE611 #" + DOM2_611_Retry;
                if (DOM2_611_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE611");
                }
                return;
            }
        }

        private void BGW_Node_612_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(300);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "612" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM2_612_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM2_612_value && DOM2_612_value != -1)
                {
                    LineNotify("Node612 score " + DOM2_612_value + " -> " + health_filed);
                    DOM2_612_value = health_filed;
                    Health_612_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_612_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM2_612_value == -1)
                {
                    DOM2_612_value = health_filed;
                    Health_612_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_612_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM2_612_Retry++;
                Status_bar.Text = "Cann't get health NODE612 #" + DOM2_612_Retry;
                if (DOM2_612_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE612");
                }
                return;
            }
        }

        //====================================== BGW DOM3 ======================================

        private void BGW_Node_221_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(50);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "221" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM3_221_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM3_221_value && DOM3_221_value != -1)
                {
                    LineNotify("Node221 score " + DOM3_221_value + " -> " + health_filed);
                    DOM3_221_value = health_filed;
                    Health_221_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_221_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM3_221_value == -1)
                {
                    DOM3_221_value = health_filed;
                    Health_221_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_221_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM3_221_Retry++;
                Status_bar.Text = "Cann't get health NODE221 #" + DOM3_221_Retry;
                if (DOM3_221_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE221");
                }
                return;
            }
        }

        private void BGW_Node_222_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(100);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "222" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM3_222_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM3_222_value && DOM3_222_value != -1)
                {
                    LineNotify("Node222 score " + DOM3_222_value + " -> " + health_filed);
                    DOM3_222_value = health_filed;
                    Health_222_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_222_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM3_222_value == -1)
                {
                    DOM3_222_value = health_filed;
                    Health_222_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_222_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM3_222_Retry++;
                Status_bar.Text = "Cann't get health NODE222 #" + DOM3_222_Retry;
                if (DOM3_222_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE222");
                }
                return;
            }
        }

        private void BGW_Node_421_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(150);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "421" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM3_421_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM3_421_value && DOM3_421_value != -1)
                {
                    LineNotify("Node421 score " + DOM3_421_value + " -> " + health_filed);
                    DOM3_421_value = health_filed;
                    Health_421_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_421_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM3_421_value == -1)
                {
                    DOM3_421_value = health_filed;
                    Health_421_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_421_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM3_421_Retry++;
                Status_bar.Text = "Cann't get health NODE421 #" + DOM3_421_Retry;
                if (DOM3_421_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE421");
                }
                return;
            }
        }

        private void BGW_Node_422_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(200);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "422" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM3_422_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM3_422_value && DOM3_422_value != -1)
                {
                    LineNotify("Node422 score " + DOM3_422_value + " -> " + health_filed);
                    DOM3_422_value = health_filed;
                    Health_422_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_422_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM3_422_value == -1)
                {
                    DOM3_422_value = health_filed;
                    Health_422_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_422_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM3_422_Retry++;
                Status_bar.Text = "Cann't get health NODE422 #" + DOM3_422_Retry;
                if (DOM3_422_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE422");
                }
                return;
            }
        }

        private void BGW_Node_621_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(250);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "621" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM3_621_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM3_621_value && DOM3_621_value != -1)
                {
                    LineNotify("Node621 score " + DOM3_621_value + " -> " + health_filed);
                    DOM3_621_value = health_filed;
                    Health_621_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_621_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM3_621_value == -1)
                {
                    DOM3_621_value = health_filed;
                    Health_621_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_621_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM3_621_Retry++;
                Status_bar.Text = "Cann't get health NODE621 #" + DOM3_621_Retry;
                if (DOM3_621_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE621");
                }
                return;
            }
        }

        private void BGW_Node_622_DoWork(object sender, DoWorkEventArgs e)
        {
            RestClient client_local = new RestClient();
            client_local.CookieContainer = client.CookieContainer;
            //Thread.Sleep(300);

            int health_filed;
            RestRequest request = new RestRequest();
            client_local.BaseUrl = new System.Uri("https://" + apicIP + "/api/node/mo/topology/pod-1/node-" + "622" + "/sys/health.json");
            request = new RestRequest(Method.GET);
            ServicePointManager.ServerCertificateValidationCallback += (RestRequest, certificate, chain, sslPolicyErrors) => true;
            request.AddHeader("cache-control", "no-cache");
            IRestResponse response1 = client_local.Execute(request);
            try
            {
                DOM3_622_Retry = 0;
                JObject datastat = JObject.Parse(response1.Content);
                health_filed = int.Parse((datastat["imdata"][0]["healthInst"]["attributes"]["twScore"].ToString()));
                if (health_filed != DOM3_622_value && DOM3_622_value != -1)
                {
                    LineNotify("Node622 score " + DOM3_622_value + " -> " + health_filed);
                    DOM3_622_value = health_filed;
                    Health_622_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_622_Score.Text = health_filed.ToString();
                    }));
                }
                if (DOM3_622_value == -1)
                {
                    DOM3_622_value = health_filed;
                    Health_622_Score.BeginInvoke(new MethodInvoker(delegate
                    {
                        Health_622_Score.Text = health_filed.ToString();
                    }));
                }
            }
            catch
            {
                DOM3_622_Retry++;
                Status_bar.Text = "Cann't get health NODE622 #" + DOM3_622_Retry;
                if (DOM3_622_Retry > MAX_RETRY)
                {
                    LineNotify("Cann't get health NODE622");
                }
                return;
            }
        }

        //====================================== TIMER BGW ======================================

        private void Timer_SPINE_BGW_Tick(object sender, EventArgs e)
        {
            BGW_POD1.WorkerReportsProgress = true;
            BGW_POD1.WorkerSupportsCancellation = true;
            if (BGW_POD1.IsBusy != true)
                BGW_POD1.RunWorkerAsync();

            BGW_Spine_101.WorkerReportsProgress = true;
            BGW_Spine_101.WorkerSupportsCancellation = true;
            if (BGW_Spine_101.IsBusy != true)
                BGW_Spine_101.RunWorkerAsync();

            BGW_Spine_102.WorkerReportsProgress = true;
            BGW_Spine_102.WorkerSupportsCancellation = true;
            if (BGW_Spine_102.IsBusy != true)
                BGW_Spine_102.RunWorkerAsync();

            BGW_Spine_301.WorkerReportsProgress = true;
            BGW_Spine_301.WorkerSupportsCancellation = true;
            if (BGW_Spine_301.IsBusy != true)
                BGW_Spine_301.RunWorkerAsync();

            BGW_Spine_302.WorkerReportsProgress = true;
            BGW_Spine_302.WorkerSupportsCancellation = true;
            if (BGW_Spine_302.IsBusy != true)
                BGW_Spine_302.RunWorkerAsync();

            BGW_Spine_501.WorkerReportsProgress = true;
            BGW_Spine_501.WorkerSupportsCancellation = true;
            if (BGW_Spine_501.IsBusy != true)
                BGW_Spine_501.RunWorkerAsync();

            BGW_Spine_502.WorkerReportsProgress = true;
            BGW_Spine_502.WorkerSupportsCancellation = true;
            if (BGW_Spine_502.IsBusy != true)
                BGW_Spine_502.RunWorkerAsync();
        }

        private void Timer_DOM1_BGW_Tick(object sender, EventArgs e)
        {
            BGW_Node_201.WorkerReportsProgress = true;
            BGW_Node_201.WorkerSupportsCancellation = true;
            if (BGW_Node_201.IsBusy != true)
                BGW_Node_201.RunWorkerAsync();

            BGW_Node_202.WorkerReportsProgress = true;
            BGW_Node_202.WorkerSupportsCancellation = true;
            if (BGW_Node_202.IsBusy != true)
                BGW_Node_202.RunWorkerAsync();

            BGW_Node_203.WorkerReportsProgress = true;
            BGW_Node_203.WorkerSupportsCancellation = true;
            if (BGW_Node_203.IsBusy != true)
                BGW_Node_203.RunWorkerAsync();

            BGW_Node_204.WorkerReportsProgress = true;
            BGW_Node_204.WorkerSupportsCancellation = true;
            if (BGW_Node_204.IsBusy != true)
                BGW_Node_204.RunWorkerAsync();

            BGW_Node_401.WorkerReportsProgress = true;
            BGW_Node_401.WorkerSupportsCancellation = true;
            if (BGW_Node_401.IsBusy != true)
                BGW_Node_401.RunWorkerAsync();

            BGW_Node_402.WorkerReportsProgress = true;
            BGW_Node_402.WorkerSupportsCancellation = true;
            if (BGW_Node_402.IsBusy != true)
                BGW_Node_402.RunWorkerAsync();

            BGW_Node_403.WorkerReportsProgress = true;
            BGW_Node_403.WorkerSupportsCancellation = true;
            if (BGW_Node_403.IsBusy != true)
                BGW_Node_403.RunWorkerAsync();

            BGW_Node_404.WorkerReportsProgress = true;
            BGW_Node_404.WorkerSupportsCancellation = true;
            if (BGW_Node_404.IsBusy != true)
                BGW_Node_404.RunWorkerAsync();
        }

        private void Timer_DOM2_BGW_Tick(object sender, EventArgs e)
        {
            BGW_Node_211.WorkerReportsProgress = true;
            BGW_Node_211.WorkerSupportsCancellation = true;
            if (BGW_Node_211.IsBusy != true)
                BGW_Node_211.RunWorkerAsync();

            BGW_Node_212.WorkerReportsProgress = true;
            BGW_Node_212.WorkerSupportsCancellation = true;
            if (BGW_Node_212.IsBusy != true)
                BGW_Node_212.RunWorkerAsync();

            BGW_Node_411.WorkerReportsProgress = true;
            BGW_Node_411.WorkerSupportsCancellation = true;
            if (BGW_Node_411.IsBusy != true)
                BGW_Node_411.RunWorkerAsync();

            BGW_Node_412.WorkerReportsProgress = true;
            BGW_Node_412.WorkerSupportsCancellation = true;
            if (BGW_Node_412.IsBusy != true)
                BGW_Node_412.RunWorkerAsync();

            BGW_Node_611.WorkerReportsProgress = true;
            BGW_Node_611.WorkerSupportsCancellation = true;
            if (BGW_Node_611.IsBusy != true)
                BGW_Node_611.RunWorkerAsync();

            BGW_Node_612.WorkerReportsProgress = true;
            BGW_Node_612.WorkerSupportsCancellation = true;
            if (BGW_Node_612.IsBusy != true)
                BGW_Node_612.RunWorkerAsync();
        }

        private void Timer_DOM3_BGW_Tick(object sender, EventArgs e)
        {
            BGW_Node_221.WorkerReportsProgress = true;
            BGW_Node_221.WorkerSupportsCancellation = true;
            if (BGW_Node_221.IsBusy != true)
                BGW_Node_221.RunWorkerAsync();

            BGW_Node_222.WorkerReportsProgress = true;
            BGW_Node_222.WorkerSupportsCancellation = true;
            if (BGW_Node_222.IsBusy != true)
                BGW_Node_222.RunWorkerAsync();

            BGW_Node_421.WorkerReportsProgress = true;
            BGW_Node_421.WorkerSupportsCancellation = true;
            if (BGW_Node_421.IsBusy != true)
                BGW_Node_421.RunWorkerAsync();

            BGW_Node_422.WorkerReportsProgress = true;
            BGW_Node_422.WorkerSupportsCancellation = true;
            if (BGW_Node_422.IsBusy != true)
                BGW_Node_422.RunWorkerAsync();

            BGW_Node_621.WorkerReportsProgress = true;
            BGW_Node_621.WorkerSupportsCancellation = true;
            if (BGW_Node_621.IsBusy != true)
                BGW_Node_621.RunWorkerAsync();

            BGW_Node_622.WorkerReportsProgress = true;
            BGW_Node_622.WorkerSupportsCancellation = true;
            if (BGW_Node_622.IsBusy != true)
                BGW_Node_622.RunWorkerAsync();
        }

        //====================================== HEALTH POD1 ======================================

        private void Health_POD1_score_TextChanged(object sender, EventArgs e)
        {
            if (Health_POD1_score.Text == "N/A")
                Health_POD1_score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_POD1_score.Text) >= MIN_GREEN_SCORE)
                Health_POD1_score.ForeColor = Color.Green;
            else if (int.Parse(Health_POD1_score.Text) >= MIN_YELLOW_SCORE)
                Health_POD1_score.ForeColor = Color.Yellow;
            else
                Health_POD1_score.ForeColor = Color.Red;
        }

        //====================================== HEALTH SPINE ======================================

        private void Health_101_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_101_Score.Text == "N/A")
                Health_101_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_101_Score.Text) >= MIN_GREEN_SCORE)
                Health_101_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_101_Score.Text) >= MIN_YELLOW_SCORE)
                Health_101_Score.ForeColor = Color.Yellow;
            else
                Health_101_Score.ForeColor = Color.Red;
        }

        private void Health_102_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_102_Score.Text == "N/A")
                Health_102_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_102_Score.Text) >= MIN_GREEN_SCORE)
                Health_102_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_102_Score.Text) >= MIN_YELLOW_SCORE)
                Health_102_Score.ForeColor = Color.Yellow;
            else
                Health_102_Score.ForeColor = Color.Red;
        }

        private void Health_301_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_301_Score.Text == "N/A")
                Health_301_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_301_Score.Text) >= MIN_GREEN_SCORE)
                Health_301_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_301_Score.Text) >= MIN_YELLOW_SCORE)
                Health_301_Score.ForeColor = Color.Yellow;
            else
                Health_301_Score.ForeColor = Color.Red;
        }

        private void Health_302_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_302_Score.Text == "N/A")
                Health_302_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_302_Score.Text) >= MIN_GREEN_SCORE)
                Health_302_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_302_Score.Text) >= MIN_YELLOW_SCORE)
                Health_302_Score.ForeColor = Color.Yellow;
            else
                Health_302_Score.ForeColor = Color.Red;
        }

        private void Health_501_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_501_Score.Text == "N/A")
                Health_501_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_501_Score.Text) >= MIN_GREEN_SCORE)
                Health_501_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_501_Score.Text) >= MIN_YELLOW_SCORE)
                Health_501_Score.ForeColor = Color.Yellow;
            else
                Health_501_Score.ForeColor = Color.Red;
        }

        private void Health_502_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_502_Score.Text == "N/A")
                Health_502_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_502_Score.Text) >= MIN_GREEN_SCORE)
                Health_502_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_502_Score.Text) >= MIN_YELLOW_SCORE)
                Health_502_Score.ForeColor = Color.Yellow;
            else
                Health_502_Score.ForeColor = Color.Red;
        }

        //====================================== HEALTH DOM1 ======================================

        private void Health_201_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_201_Score.Text == "N/A")
                Health_201_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_201_Score.Text) >= MIN_GREEN_SCORE)
                Health_201_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_201_Score.Text) >= MIN_YELLOW_SCORE)
                Health_201_Score.ForeColor = Color.Yellow;
            else
                Health_201_Score.ForeColor = Color.Red;
        }

        private void Health_202_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_202_Score.Text == "N/A")
                Health_202_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_202_Score.Text) >= MIN_GREEN_SCORE)
                Health_202_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_202_Score.Text) >= MIN_YELLOW_SCORE)
                Health_202_Score.ForeColor = Color.Yellow;
            else
                Health_202_Score.ForeColor = Color.Red;
        }

        private void Health_203_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_203_Score.Text == "N/A")
                Health_203_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_203_Score.Text) >= MIN_GREEN_SCORE)
                Health_203_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_203_Score.Text) >= MIN_YELLOW_SCORE)
                Health_203_Score.ForeColor = Color.Yellow;
            else
                Health_203_Score.ForeColor = Color.Red;
        }


        private void Health_204_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_204_Score.Text == "N/A")
                Health_204_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_204_Score.Text) >= MIN_GREEN_SCORE)
                Health_204_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_204_Score.Text) >= MIN_YELLOW_SCORE)
                Health_204_Score.ForeColor = Color.Yellow;
            else
                Health_204_Score.ForeColor = Color.Red;
        }

        private void Health_401_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_401_Score.Text == "N/A")
                Health_401_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_401_Score.Text) >= MIN_GREEN_SCORE)
                Health_401_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_401_Score.Text) >= MIN_YELLOW_SCORE)
                Health_401_Score.ForeColor = Color.Yellow;
            else
                Health_401_Score.ForeColor = Color.Red;
        }

        private void Health_402_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_402_Score.Text == "N/A")
                Health_402_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_402_Score.Text) >= MIN_GREEN_SCORE)
                Health_402_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_402_Score.Text) >= MIN_YELLOW_SCORE)
                Health_402_Score.ForeColor = Color.Yellow;
            else
                Health_402_Score.ForeColor = Color.Red;
        }

        private void Health_403_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_403_Score.Text == "N/A")
                Health_403_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_403_Score.Text) >= MIN_GREEN_SCORE)
                Health_403_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_403_Score.Text) >= MIN_YELLOW_SCORE)
                Health_403_Score.ForeColor = Color.Yellow;
            else
                Health_403_Score.ForeColor = Color.Red;
        }

        private void Health_404_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_404_Score.Text == "N/A")
                Health_404_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_404_Score.Text) >= MIN_GREEN_SCORE)
                Health_404_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_404_Score.Text) >= MIN_YELLOW_SCORE)
                Health_404_Score.ForeColor = Color.Yellow;
            else
                Health_404_Score.ForeColor = Color.Red;
        }

        //====================================== HEALTH DOM2 ======================================

        private void Health_211_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_211_Score.Text == "N/A")
                Health_211_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_211_Score.Text) >= MIN_GREEN_SCORE)
                Health_211_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_211_Score.Text) >= MIN_YELLOW_SCORE)
                Health_211_Score.ForeColor = Color.Yellow;
            else
                Health_211_Score.ForeColor = Color.Red;
        }

        private void Health_212_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_212_Score.Text == "N/A")
                Health_212_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_212_Score.Text) >= MIN_GREEN_SCORE)
                Health_212_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_212_Score.Text) >= MIN_YELLOW_SCORE)
                Health_212_Score.ForeColor = Color.Yellow;
            else
                Health_212_Score.ForeColor = Color.Red;
        }

        private void Health_411_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_411_Score.Text == "N/A")
                Health_411_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_411_Score.Text) >= MIN_GREEN_SCORE)
                Health_411_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_411_Score.Text) >= MIN_YELLOW_SCORE)
                Health_411_Score.ForeColor = Color.Yellow;
            else
                Health_411_Score.ForeColor = Color.Red;
        }

        private void Health_412_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_412_Score.Text == "N/A")
                Health_412_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_412_Score.Text) >= MIN_GREEN_SCORE)
                Health_412_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_412_Score.Text) >= MIN_YELLOW_SCORE)
                Health_412_Score.ForeColor = Color.Yellow;
            else
                Health_412_Score.ForeColor = Color.Red;
        }

        private void Health_611_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_611_Score.Text == "N/A")
                Health_611_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_611_Score.Text) >= MIN_GREEN_SCORE)
                Health_611_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_611_Score.Text) >= MIN_YELLOW_SCORE)
                Health_611_Score.ForeColor = Color.Yellow;
            else
                Health_611_Score.ForeColor = Color.Red;
        }

        private void Health_612_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_612_Score.Text == "N/A")
                Health_612_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_612_Score.Text) >= MIN_GREEN_SCORE)
                Health_612_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_612_Score.Text) >= MIN_YELLOW_SCORE)
                Health_612_Score.ForeColor = Color.Yellow;
            else
                Health_612_Score.ForeColor = Color.Red;
        }

        //====================================== HEALTH DOM3 ======================================

        private void Health_221_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_221_Score.Text == "N/A")
                Health_221_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_221_Score.Text) >= MIN_GREEN_SCORE)
                Health_221_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_221_Score.Text) >= MIN_YELLOW_SCORE)
                Health_221_Score.ForeColor = Color.Yellow;
            else
                Health_221_Score.ForeColor = Color.Red;
        }

        private void Health_222_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_222_Score.Text == "N/A")
                Health_222_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_222_Score.Text) >= MIN_GREEN_SCORE)
                Health_222_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_222_Score.Text) >= MIN_YELLOW_SCORE)
                Health_222_Score.ForeColor = Color.Yellow;
            else
                Health_222_Score.ForeColor = Color.Red;
        }

        private void Health_421_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_421_Score.Text == "N/A")
                Health_421_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_421_Score.Text) >= MIN_GREEN_SCORE)
                Health_421_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_421_Score.Text) >= MIN_YELLOW_SCORE)
                Health_421_Score.ForeColor = Color.Yellow;
            else
                Health_421_Score.ForeColor = Color.Red;
        }

        private void Health_422_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_422_Score.Text == "N/A")
                Health_422_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_422_Score.Text) >= MIN_GREEN_SCORE)
                Health_422_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_422_Score.Text) >= MIN_YELLOW_SCORE)
                Health_422_Score.ForeColor = Color.Yellow;
            else
                Health_422_Score.ForeColor = Color.Red;
        }

        private void Health_621_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_621_Score.Text == "N/A")
                Health_621_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_621_Score.Text) >= MIN_GREEN_SCORE)
                Health_621_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_621_Score.Text) >= MIN_YELLOW_SCORE)
                Health_621_Score.ForeColor = Color.Yellow;
            else
                Health_621_Score.ForeColor = Color.Red;
        }

        private void Health_622_Score_TextChanged(object sender, EventArgs e)
        {
            if (Health_622_Score.Text == "N/A")
                Health_622_Score.ForeColor = Color.Yellow;
            else if (int.Parse(Health_622_Score.Text) >= MIN_GREEN_SCORE)
                Health_622_Score.ForeColor = Color.Green;
            else if (int.Parse(Health_622_Score.Text) >= MIN_YELLOW_SCORE)
                Health_622_Score.ForeColor = Color.Yellow;
            else
                Health_622_Score.ForeColor = Color.Red;
        }
    }
}
