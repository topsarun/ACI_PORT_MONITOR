using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;
using System.Collections.Specialized;

namespace ACI_PORT_MONITOR
{
    public partial class Setting_Form : Form
    {
        string apicIP = ConfigurationManager.AppSettings.Get("APIC-1_IP");
        string apic_id = ConfigurationManager.AppSettings.Get("APIC_ID");
        string apic_pw = ConfigurationManager.AppSettings.Get("APIC_PASS");
        string line_token = ConfigurationManager.AppSettings.Get("Line_token");
        int MAX_RETRY = int.Parse(ConfigurationManager.AppSettings.Get("MAX_RETRY"));

        public Setting_Form()
        {
            InitializeComponent();
            apicIP_Box.Text = apicIP;
            apic_id_Box.Text = apic_id;
            apic_pw_Box.Text = apic_pw;
            line_token_Box.Text = line_token;
            MAX_RETRY_Box.Text = MAX_RETRY.ToString();
        }

        private void UPDATE_button_Click(object sender, EventArgs e)
        {
            var confirmResult = MessageBox.Show("Are you sure ??", "Confirm !!", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirmResult == DialogResult.Yes)
            {
                ConfigurationManager.AppSettings.Set("APIC-1_IP", apicIP_Box.Text);
                ConfigurationManager.AppSettings.Set("APIC_ID", apic_id_Box.Text);
                ConfigurationManager.AppSettings.Set("APIC_PASS", apic_pw_Box.Text);
                ConfigurationManager.AppSettings.Set("Line_token", line_token_Box.Text);
                ConfigurationManager.AppSettings.Set("MAX_RETRY", MAX_RETRY_Box.Text);
            }
        }
    }
}
