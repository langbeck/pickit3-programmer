using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Pk2 = PICkit2V2.PICkitFunctions;

namespace PICkit2V2
{
    public partial class DialogPK2Go : Form
    {
        public float VDDVolts = 0;
        public string dataSource = "--";
        public bool codeProtect = false;
        public bool dataProtect = false;
        public bool verifyDevice = false;
        public bool vppFirst = false;
        public bool writeProgMem = true;
        public bool writeEEPROM = true;
        public bool fastProgramming = true;
        public bool holdMCLR = false;
        public byte icspSpeedSlow = 4;

        private byte ptgMemory = 0; // 128K default
        
        private int blinkCount = 0;
    
        public DialogPK2Go()
        {
            InitializeComponent();
        }
        
        public void SetPTGMemory(byte value)
        {
            ptgMemory = value;
            if ((ptgMemory > 0) && (ptgMemory <= 5))
                label256K.Visible = true;
            //===== Display what will be used for PTG =====
            if (ptgMemory == 1) label256K.Text = "256K PICkit 2 upgrade support enabled.\r\n";
            else if (ptgMemory == 2) label256K.Text = "512K SPI memory support enabled.\r\n";
            else if (ptgMemory == 3) label256K.Text = "1M SPI memory support enabled.\r\n";
            else if (ptgMemory == 4) label256K.Text = "2M SPI memory support enabled.\r\n";
            else if (ptgMemory == 5) label256K.Text = "4M SPI memory support enabled.\r\n";
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void buttonNext_Click(object sender, EventArgs e)
        {
            if (panelIntro.Visible)
            {
                panelIntro.Visible = false;
                buttonBack.Enabled = true;
                fillSettings(true);
            }
            else if (panelSettings.Visible)
            {
                if (checkEraseVoltage())
                {
                    panelSettings.Visible = false;
                    buttonNext.Text = "Download";
                    fillDownload();
                }
            }
            else if (panelDownload.Visible)
            {
                downloadGO();
            }       
            else if (panelDownloadDone.Visible)
            {
                buttonNext.Enabled = false;
                panelDownloadDone.Visible = false;
                panelErrors.Visible = true;
                timerBlink.Interval = 84;
            }     
        }

        private void buttonBack_Click(object sender, EventArgs e)
        {
            if (panelSettings.Visible)
            {
                panelSettings.Visible = false;
                panelIntro.Visible = true;
                buttonBack.Enabled = false;
            }
            else if (panelDownload.Visible)
            {
                panelDownload.Visible = false;
                buttonNext.Text = "Next >";
                fillSettings(false);
            }
        }
        
        private bool checkEraseVoltage()
        {
            if (radioButtonSelfPower.Checked)
                return true;
            if (VDDVolts < Pk2.DevFile.PartsList[Pk2.ActivePart].VddErase)
            {
                if (Pk2.DevFile.PartsList[Pk2.ActivePart].DebugRowEraseScript == 0)
                {
                    DialogResult goAhead = MessageBox.Show(
                        "The selected PICkit 2 VDD voltage is below\nthe minimum required to Bulk Erase this part.\n\nContinue anyway?",
                        labelPartNumber.Text + " VDD Error", MessageBoxButtons.OKCancel);
                        
                    if (goAhead == DialogResult.OK)
                        return true;
                    else
                        return false;
                }
            }
            return true;
        }
        
        private void fillSettings(bool changePower)
        {
            // Buffer settings
            // Device
            labelPartNumber.Text = Pk2.DevFile.PartsList[Pk2.ActivePart].PartName;
            if (Pk2.DevFile.PartsList[Pk2.ActivePart].OSSCALSave)
            {
                labelOSCCAL_BandGap.Visible = true;
                if (Pk2.DevFile.PartsList[Pk2.ActivePart].BandGapMask > 0)
                {
                    labelOSCCAL_BandGap.Text = "OSCCAL && BandGap will be preserved.";
                }

            }
            // source
            if (dataSource == "Edited.")
                labelDataSource.Text = "Edited Buffer.";
            else
                labelDataSource.Text = dataSource;
            // code protects
            if (!writeProgMem)
            { // write EE only
                labelCodeProtect.Text = "N/A";
                labelDataProtect.Text = "N/A";
            }
            else
            {
                if (codeProtect)
                    labelCodeProtect.Text = "ON";
                else
                    labelCodeProtect.Text = "OFF";
                if (dataProtect)
                    labelDataProtect.Text = "ON";
                else
                {
                    if (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem > 0)
                        labelDataProtect.Text = "OFF";                
                    else
                        labelDataProtect.Text = "N/A";
                }
            }
            // mem regions
            if (!writeProgMem)
            {
                labelMemRegions.Text = "Write EEPROM data only.";
            }
            else if (!writeEEPROM && (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem > 0))
            {
                labelMemRegions.Text = "Preserve EEPROM on write.";
            }
            else
            {
                labelMemRegions.Text = "Write entire device.";
            } 
            if (verifyDevice)
                labelVerify.Text = "Yes";
            else
                labelVerify.Text = "No - device will NOT be verified";
            
            // Power Settings
            if (changePower)
            {
                radioButtonPK2Power.Text = string.Format("Power target from PICkit 2 at {0:0.0} Volts.", VDDVolts);
                if (vppFirst)
                {
                    radioButtonSelfPower.Enabled = false;
                    radioButtonSelfPower.Text = "Use VPP First - must power from PICkit 2";
                    checkBoxRowErase.Enabled = false;
                    radioButtonPK2Power.Checked = true;
                    pickit2PowerRowErase();
                }
                else
                {
                    radioButtonSelfPower.Checked = true;
                    if (Pk2.DevFile.PartsList[Pk2.ActivePart].DebugRowEraseScript > 0)
                    {
                        checkBoxRowErase.Text = string.Format("VDD < {0:0.0}V: Use low voltage row erase", 
                                    Pk2.DevFile.PartsList[Pk2.ActivePart].VddErase);
                        checkBoxRowErase.Enabled = true;
                    }
                    else
                    {
                        checkBoxRowErase.Visible = false;
                        checkBoxRowErase.Enabled = false;
                        labelVDDMin.Text = string.Format("VDD must be >= {0:0.0} Volts.", Pk2.DevFile.PartsList[Pk2.ActivePart].VddErase);
                        labelVDDMin.Visible = true;
                    }
                }
            }
            panelSettings.Visible = true;
        }
        
        private bool pickit2PowerRowErase()
        {
            if (VDDVolts < Pk2.DevFile.PartsList[Pk2.ActivePart].VddErase)
            {
                if (Pk2.DevFile.PartsList[Pk2.ActivePart].DebugRowEraseScript > 0)
                {
                    labelRowErase.Text = "Row Erase used: Will NOT program Code Protected parts!";
                    labelRowErase.Visible = true;
                }
                else
                {
                    MessageBox.Show(string.Format("PICkit 2 cannot program this device\nat the selected VDD voltage.\n\n{0:0.0}V is below the minimum for erase, {0:0.0}V", 
                        VDDVolts, Pk2.DevFile.PartsList[Pk2.ActivePart].VddErase), "Programmer-To-Go");
                    return false;
                }
            }
            else
            {
                labelRowErase.Visible = false;
            }
            return true;
        }
        
        private void fillDownload()
        {
            labelPNsmmry.Text = labelPartNumber.Text;
            labelSourceSmmry.Text = labelDataSource.Text;
            
            if (radioButtonSelfPower.Checked)
            {
                if (checkBoxRowErase.Enabled && checkBoxRowErase.Checked)
                {
                    labelTargetPowerSmmry.Text = "Target is Powered (Use Low Voltage Row Erase)";
                }
                else
                {
                    labelTargetPowerSmmry.Text = string.Format("Target is Powered (Min VDD = {0:0.0} Volts)", Pk2.DevFile.PartsList[Pk2.ActivePart].VddErase);
                }
            }
            else
            {
                labelTargetPowerSmmry.Text = string.Format("Power target from PICkit 2 at {0:0.0} Volts", VDDVolts);
            }
            
            labelMemRegionsSmmry.Text = labelMemRegions.Text;
            
            if (writeProgMem)
            {
                if (codeProtect)
                    labelMemRegionsSmmry.Text += " -CP";
                if (dataProtect)
                    labelMemRegionsSmmry.Text += " -DP";
            }
            
            if (vppFirst)
                labelVPP1stSmmry.Text = "Use VPP 1st Program Entry";
            else
                labelVPP1stSmmry.Text = "";
                
            if (verifyDevice)
                labelVerifySmmry.Text = "Device will be verified";
            else
                labelVerifySmmry.Text = "Device will NOT be verified";
                
            if (fastProgramming)
                labelFastProgSmmry.Text = "Fast Programming is ON";
            else
                labelFastProgSmmry.Text = "Fast Programming is OFF";
                
            if (holdMCLR)
                labelMCLRHoldSmmry.Text = "MCLR kept asserted during && after programming";
            else
                labelMCLRHoldSmmry.Text = "MCLR released after programming";
            
            panelDownload.Visible = true;
        }
        
        public DelegateWrite PICkit2WriteGo;
        
        private void downloadGO()
        {
            panelDownload.Visible = false;
            panelDownloading.Visible = true;
            buttonHelp.Enabled = false;
            buttonBack.Enabled = false;
            buttonNext.Enabled = false;
            buttonCancel.Enabled = false;
            buttonCancel.Text = "Exit";
            this.Update();
        
            if (radioButtonSelfPower.Checked)
            {
                Pk2.ForceTargetPowered();
            }
            else
            {
                Pk2.ForcePICkitPowered();
            }
            if (ptgMemory <= 5)
                Pk2.EnterLearnMode(ptgMemory); // set memory size to use
            else
                Pk2.EnterLearnMode(0); // default to 128K on illegal value
            
            if (fastProgramming)
                Pk2.SetProgrammingSpeed(0);
            else
                Pk2.SetProgrammingSpeed(icspSpeedSlow);
            
            PICkit2WriteGo(true);
            
            Pk2.ExitLearnMode();
            
            if (ptgMemory <= 5)
                Pk2.EnablePK2GoMode(ptgMemory); // set memory size to use
            else
                Pk2.EnablePK2GoMode(0); // default to 128K on illegal value.

            Pk2.DisconnectPICkit2Unit();
            
            panelDownloading.Visible = false;
            panelDownloadDone.Visible = true;
            buttonHelp.Enabled = true;
            buttonNext.Enabled = true;
            buttonNext.Text = "Next >";
            buttonCancel.Enabled = true;      
            timerBlink.Enabled = true;      
        }

        private void radioButtonPK2Power_Click(object sender, EventArgs e)
        {
            radiobuttonPower();
        }

        private void radioButtonSelfPower_Click(object sender, EventArgs e)
        {
            radiobuttonPower();
        }        
        
        private void radiobuttonPower()
        {
                    if (radioButtonPK2Power.Checked)
            {
                checkBoxRowErase.Enabled = false;
                if (!pickit2PowerRowErase())
                {
                    radioButtonPK2Power.Checked = false;
                    radioButtonSelfPower.Checked = true;
                }
            }
            else
            {
                if (Pk2.DevFile.PartsList[Pk2.ActivePart].DebugRowEraseScript > 0)
                {
                    checkBoxRowErase.Enabled = true;
                }
                else
                {
                    checkBoxRowErase.Enabled = false;
                }
            
                if (checkBoxRowErase.Enabled && checkBoxRowErase.Checked)
                {
                    labelRowErase.Text = "Row Erase used: Will NOT program Code Protected parts!";
                    labelRowErase.Visible = true;
                }
                else
                {
                    labelRowErase.Visible = false;
                }
            }
        }

        private void checkBoxRowErase_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxRowErase.Enabled && checkBoxRowErase.Checked)
            {
                labelRowErase.Text = "Row Erase used: Will NOT program Code Protected parts!";
                labelRowErase.Visible = true;
            }
            else
            {
                labelRowErase.Visible = false;
            }
        }

        private void timerBlink_Tick(object sender, EventArgs e)
        {
            if (panelDownloadDone.Visible)
            {
                blinkCount++;
                if (blinkCount > 5)
                    blinkCount = 0;
                if (blinkCount < 4)
                {
                    if ((blinkCount & 0x1) == 0)
                        pictureBoxTarget.BackColor = Color.Yellow;
                    else
                        pictureBoxTarget.BackColor = System.Drawing.SystemColors.ControlText;
                }
            }
            else
            { // error panel
                if (radioButtonVErr.Checked)
                {
                    blinkCount++;
                    if ((blinkCount & 0x1) == 0)
                        pictureBoxBusy.BackColor = Color.Red;
                    else
                        pictureBoxBusy.BackColor = System.Drawing.SystemColors.ControlText;
                }
                else
                {
                    int blink = 4;
                    if (radioButton3Blinks.Checked)
                        blink = 6;
                    else if (radioButton4Blinks.Checked)
                        blink = 8;
                    if (blinkCount++ <= blink)
                    {
                        if ((blinkCount & 0x1) == 0)
                            pictureBoxBusy.BackColor = Color.Red;
                        else
                            pictureBoxBusy.BackColor = System.Drawing.SystemColors.ControlText;
                    }
                    else
                        blinkCount = 0;
                }   
            }

        }

        private void DialogPK2Go_FormClosing(object sender, FormClosingEventArgs e)
        {
            Pk2.ExitLearnMode(); // just in case.
        }

        private void radioButtonVErr_Click(object sender, EventArgs e)
        {
            if (radioButtonVErr.Checked)
                timerBlink.Interval = 84;
            else
                timerBlink.Interval = 200;
        }

        public DelegateOpenProgToGoGuide OpenProgToGoGuide;

        private void buttonHelp_Click(object sender, EventArgs e)
        {
            OpenProgToGoGuide();
        }


    }
}