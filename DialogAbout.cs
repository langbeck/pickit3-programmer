using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace PICkit2V2
{
    public partial class DialogAbout : Form
    {
        public DialogAbout()
        {
            InitializeComponent();
            displayAppVer.Text =  Constants.AppVersion;
            displayDevFileVer.Text = PICkitFunctions.DeviceFileVersion;
            displayPk2FWVer.Text = PICkitFunctions.FirmwareVersion;
            textBox1.Select(0,0);
			if (!PICkitFunctions.isPK3)
			{
				label2.Text = "2";
			}
        }

        private void clickOK(object sender, EventArgs e)
        {
            this.Close();
        }

        private void microchipLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                visitMicrochipSite();
            }
            catch
            {
                MessageBox.Show("Unable to open link that was clicked.");
            }

        }

        private void visitMicrochipSite()
        {
            // Change the color of the link text by setting LinkVisited 
            // to true.
            linkLabel1.LinkVisited = true;
            //Call the Process.Start method to open the default browser 
            //with a URL:
            System.Diagnostics.Process.Start("http://www.microchip.com");

        }
    }
}