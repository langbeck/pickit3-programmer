using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using Pk2 = PICkit2V2.PICkitFunctions;
using KONST = PICkit2V2.Constants;
using UTIL = PICkit2V2.Utilities;
using System.IO;

namespace PICkit2V2
{
    public partial class DialogUART : Form
    {
        public DelegateVddCallback VddCallback;
    
        public static string CustomBaud = "";
        
        private struct baudTable
        {
            public string baudRate;
            public uint baudValue;
        }
        private baudTable[] baudList;
        StreamWriter logFile = null;
        private bool newRX = true;
        private int hex1Length = 0;
        private int hex2Length = 0;
        private int hex3Length = 0;
        private int hex4Length = 0;
        
        public DialogUART()
        {
            InitializeComponent();
            this.KeyPress += new KeyPressEventHandler(OnKeyPress);
            
            baudList = new baudTable[7];
            baudList[0].baudRate = "300";
            baudList[0].baudValue = 0xB1F2;            
            baudList[1].baudRate = "1200";
            baudList[1].baudValue = 0xEC8A;
            baudList[2].baudRate = "2400";
            baudList[2].baudValue = 0xF64E;
            baudList[3].baudRate = "4800";
            baudList[3].baudValue = 0xFB30;
            baudList[4].baudRate = "9600";
            baudList[4].baudValue = 0xFDA1;
            baudList[5].baudRate = "19200";
            baudList[5].baudValue = 0xFEDA;
            baudList[6].baudRate = "38400";
            baudList[6].baudValue = 0xFF76;
            
            buildBaudList();
        }
        
        public string GetBaudRate()
        {
            return comboBoxBaud.SelectedItem.ToString();
        }
        
        public bool IsHexMode()
        {
            return radioButtonHex.Checked;
        }
        
        public string GetStringMacro(int macroNum)
        {
            if (macroNum == 2)
            {
                return textBoxString2.Text;
            }
            else if (macroNum == 3)
            {
                return textBoxString3.Text;
            }
            else if (macroNum == 4)
            {
                return textBoxString4.Text;
            }
            else
            {
                return textBoxString1.Text;
            }
        }
        
        public bool GetAppendCRLF()
        {
            return checkBoxCRLF.Checked;
        }

        public bool GetWrap()
        {
            return checkBoxWrap.Checked;
        }

        public bool GetEcho()
        {
            return checkBoxEcho.Checked;
        }     
        
        public void SetBaudRate(string baudRate)
        {
            for (int i = 0; i < baudList.Length; i++)
            {
                if (baudRate == comboBoxBaud.Items[i].ToString())
                {
                    comboBoxBaud.SelectedIndex = i;
                    break;
                }
                if ((i + 1) == baudList.Length)
                {// didn't find it- must be custom
                    comboBoxBaud.Items.Add(baudRate);
                    comboBoxBaud.SelectedIndex = i + 3;
                }
            }
        }

        public void SetStringMacro(string macro, int macroNum)
        {
            if (macroNum == 2)
            {
                textBoxString2.Text = macro;
                hex1Length = macro.Length;
            }
            else if (macroNum == 3)
            {
                textBoxString3.Text = macro;
                hex2Length = macro.Length;
            }
            else if (macroNum == 4)
            {
                textBoxString4.Text = macro;
                hex3Length = macro.Length;
            }
            else
            {
                textBoxString1.Text = macro;
                hex4Length = macro.Length;
            }
        }
        
        public void SetModeHex()
        {
            radioButtonHex.Checked = true;
        }    
        
        public void ClearAppendCRLF()
        {
            checkBoxCRLF.Checked = false;
        }

        public void ClearWrap()
        {
            checkBoxWrap.Checked = false;
        }

        public void ClearEcho()
        {
            checkBoxEcho.Checked = false;
        }             
        
        public void SetVddBox(bool enable, bool check)
        {
            checkBoxVDD.Enabled = enable;
            checkBoxVDD.Checked = check;
        }
            
        private const string CUSTOM_BAUD = "Custom...";        
        private void buildBaudList()
        {
            for (int i = 0; i < baudList.Length; i++)
            {
                comboBoxBaud.Items.Add(baudList[i].baudRate);
            }
            comboBoxBaud.Items.Add(CUSTOM_BAUD);
            comboBoxBaud.SelectedIndex = 0;
        }

        private void buttonExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void DialogUART_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (logFile != null)
            {
                closeLogFile();
            }
            timerPollForData.Enabled = false;
            Pk2.ExitUARTMode();
           radioButtonConnect.Checked = false;
           radioButtonDisconnect.Checked = true;
           comboBoxBaud.Enabled = true;
           buttonString1.Enabled = false;
           buttonString2.Enabled = false;
           buttonString3.Enabled = false;
           buttonString4.Enabled = false;
           panelVdd.Enabled = true; // no VDD changes when connected
        }

        public void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            string hexChars = "0123456789ABCDEF";

            if (textBoxString1.ContainsFocus | textBoxString2.ContainsFocus
                | textBoxString3.ContainsFocus |textBoxString4.ContainsFocus)
            { // ignore typing in textboxes
                return;
            }
            
            // check for copy/cut
            if ((e.KeyChar == 3) || (e.KeyChar == 24))
            {
                textBoxDisplay.Copy();
                return;
            }

            if (radioButtonDisconnect.Checked)
            { // don't do anything else if not connected
                return;
            }

            textBoxDisplay.Focus();
            
            if (radioButtonHex.Checked)
            { // hex mode
                string charTyped = e.KeyChar.ToString();    // get typed char
                charTyped = charTyped.ToUpper();
                if (charTyped.IndexOfAny(hexChars.ToCharArray()) == 0)
                { // valid Hex character
                    if (labelTypeHex.Visible)
                    { // first nibble already typed - send byte
                        string dataString = labelTypeHex.Text.Substring(11,1) + charTyped;
                        labelTypeHex.Text = "Type Hex : ";
                        labelTypeHex.Visible = false;
                        byte[] hexByte = new byte[1];
                        hexByte[0] = (byte)Utilities.Convert_Value_To_Int("0x" + dataString);
                        dataString = "TX:  " + dataString + "\r\n";
                        textBoxDisplay.AppendText(dataString);
                        textBoxDisplay.SelectionStart = textBoxDisplay.Text.Length;
                        textBoxDisplay.ScrollToCaret();
                        if (logFile != null)
                        {
                            logFile.Write(dataString);
                        }
                        Pk2.DataDownload(hexByte, 0, hexByte.Length);
                    }
                    else
                    { // show first nibble
                        labelTypeHex.Text = "Type Hex : " + charTyped + "_";
                        labelTypeHex.Visible = true;
                    }
                }
                else
                { // other char - clear typed hex
                    labelTypeHex.Text = "Type Hex : ";
                    labelTypeHex.Visible = false;
                }
            }
            else
            { // ASCII mode
                // check for paste
                if (e.KeyChar == 22)
                {
                    textBoxDisplay.SelectionStart = textBoxDisplay.Text.Length; //cursor at end
                    TextBox tempBox = new TextBox();
                    tempBox.Multiline = true;
                    tempBox.Paste();
                    do
                    {
                        int pasteLength = tempBox.Text.Length;
                        if (pasteLength > 60)
                        {
                            pasteLength = 60;
                        }
                        sendString(tempBox.Text.Substring(0, pasteLength), false);
                        tempBox.Text = tempBox.Text.Substring(pasteLength);
                        
                        // wait according to the baud rate so we don't overflow the download buffer
                        float baud = float.Parse((comboBoxBaud.SelectedItem.ToString()));
                        baud = (1F / baud) * 12F * (float)pasteLength; // to ensure we don't overflow, give each byte 12 bits
                        baud *= 1000F; // baud is now in ms.
                        Thread.Sleep((int)baud);
                    } while (tempBox.Text.Length > 0);
                    
                    tempBox.Dispose();
                    return;
                }
                
                string charTyped = e.KeyChar.ToString();
                if (charTyped == "\r")
                {
                    charTyped = "\r\n";
                }
                sendString(charTyped, false);
            }
        }

        private void radioButtonConnect_Click_1(object sender, EventArgs e)
        {
            if (!radioButtonConnect.Checked)
            {
                if (comboBoxBaud.SelectedIndex == 0)
                {
                    MessageBox.Show("Please Select a Baud Rate.");
                    return;
                }
                uint baudValue = 0;
                for (int i = 0; i < baudList.Length; i++)
                {
                    if (comboBoxBaud.SelectedItem.ToString() == baudList[i].baudRate)
                    {
                        baudValue = baudList[i].baudValue;
                        break;
                    }
                    if ((i + 1) == baudList.Length)
                    {// didn't find it- must be custom
                        try
                        {
                            float baudRate = float.Parse(comboBoxBaud.SelectedItem.ToString());
                            baudRate = ((1F / baudRate) - 3e-6F) / 1.6667e-7F;
                            baudValue = 0x10000 - (uint)baudRate;
                        }
                        catch
                        {
                            MessageBox.Show("Error with Baud setting.");
                            return;
                        }
                    }
                }
                panelVdd.Enabled = false; // no VDD changes when connected
                Pk2.EnterUARTMode(baudValue);
                radioButtonConnect.Checked = true;
                radioButtonDisconnect.Checked = false;
                buttonString1.Enabled = true;
                buttonString2.Enabled = true;
                buttonString3.Enabled = true;
                buttonString4.Enabled = true;                
                comboBoxBaud.Enabled = false; // can't change value when connected.
                if (baudValue < 0xEC8A)
                {// 1200 or less: slower polling
                    timerPollForData.Interval = 75;
                }
                else
                { // faster
                    timerPollForData.Interval = 15;
                }
                timerPollForData.Enabled = true;
            }
        }

        private void radioButtonDisconnect_Click(object sender, EventArgs e)
        {
            if (!radioButtonDisconnect.Checked)
            {
                radioButtonConnect.Checked = false;
                radioButtonDisconnect.Checked = true;
                Pk2.ExitUARTMode();
                comboBoxBaud.Enabled = true;
                timerPollForData.Enabled = false;
                buttonString1.Enabled = false;
                buttonString2.Enabled = false;
                buttonString3.Enabled = false;
                buttonString4.Enabled = false;
                panelVdd.Enabled = true; // no VDD changes when connected
                // clear partial hex typing
                labelTypeHex.Text = "Type Hex : ";
                labelTypeHex.Visible = false;
            }
        }

        private void buttonClearScreen_Click(object sender, EventArgs e)
        {
            textBoxDisplay.Text = "";
        }

        private void timerPollForData_Tick(object sender, EventArgs e)
        {        
            Pk2.UploadData();          
            if (Pk2.Usb_read_array[1] > 0)
            {
                string newData = "";
                if (radioButtonASCII.Checked)
                {
                    newData = Encoding.ASCII.GetString(Pk2.Usb_read_array, 2, Pk2.Usb_read_array[1]);
                }
                else
                { // hex mode
                    if (newRX)
                    {
                        newData = "RX:  ";
                        newRX = false;
                    }
                    for (int b = 0; b < Pk2.Usb_read_array[1]; b++)
                    {
                        newData += string.Format("{0:X2} ", Pk2.Usb_read_array[b + 2]);
                    }
                }
                if (logFile != null)
                {
                    logFile.Write(newData);
                }
                textBoxDisplay.AppendText(newData);
                
                while (textBoxDisplay.Text.Length > 16400)
                {// about 200 lines
                    // delete a line
                    int endOfLine = textBoxDisplay.Text.IndexOf("\r\n") + 2;
                    if (endOfLine == 1)
                    {// no line found
                        endOfLine = textBoxDisplay.Text.Length - 16000; // delete several hundred chars
                    }
                    textBoxDisplay.Text = textBoxDisplay.Text.Substring(endOfLine);
                }
                textBoxDisplay.SelectionStart = textBoxDisplay.Text.Length;
                textBoxDisplay.ScrollToCaret();
            }
            else
            {
                if (!newRX && radioButtonHex.Checked)
                {
                    textBoxDisplay.AppendText("\r\n");
                    if (logFile != null)
                    {
                        logFile.Write("\r\n");
                    }
                    textBoxDisplay.SelectionStart = textBoxDisplay.Text.Length;
                    textBoxDisplay.ScrollToCaret();
                }
                newRX = true;
            }
        }
        
        private int getLastLineLength(string text)
        {
            int lastLine = text.LastIndexOf("\r\n") + 2;
            if (lastLine < 2)
            {
                lastLine = 0;
            }
            return (text.Length - lastLine);
        }

        private const int MaxLengthASCII = 60;
        private void textBoxString1_TextChanged(object sender, EventArgs e)
        {
            if ((textBoxString1.Text.Length > MaxLengthASCII) && radioButtonASCII.Checked)
            {
                textBoxString1.Text = textBoxString1.Text.Substring(0, MaxLengthASCII);
                textBoxString1.SelectionStart = MaxLengthASCII;
            }
            if (radioButtonHex.Checked)
            {
                formatHexString(textBoxString1, ref hex1Length);
            }
        }

        private void textBoxString2_TextChanged(object sender, EventArgs e)
        {
            if ((textBoxString2.Text.Length > MaxLengthASCII) && radioButtonASCII.Checked)
            {
                textBoxString2.Text = textBoxString2.Text.Substring(0, MaxLengthASCII);
                textBoxString2.SelectionStart = MaxLengthASCII;
            }
            if (radioButtonHex.Checked)
            {
                formatHexString(textBoxString2, ref hex2Length);
            }
        }

        private void textBoxString3_TextChanged(object sender, EventArgs e)
        {
            if ((textBoxString3.Text.Length > MaxLengthASCII) && radioButtonASCII.Checked)
            {
                textBoxString3.Text = textBoxString3.Text.Substring(0, MaxLengthASCII);
                textBoxString3.SelectionStart = MaxLengthASCII;
            }
            if (radioButtonHex.Checked)
            {
                formatHexString(textBoxString3, ref hex3Length);
            }
        }

        private void textBoxString4_TextChanged(object sender, EventArgs e)
        {
            if ((textBoxString4.Text.Length > MaxLengthASCII) && radioButtonASCII.Checked)
            {
                textBoxString4.Text = textBoxString4.Text.Substring(0, MaxLengthASCII);
                textBoxString4.SelectionStart = MaxLengthASCII;
            }
            if (radioButtonHex.Checked)
            {
                formatHexString(textBoxString4, ref hex4Length);
            }
        }
        private const int MaxHexLength = 143; // 48 bytes
        private void formatHexString(TextBox textBoxToFormat, ref int priorLength)
        {
            string workString = textBoxToFormat.Text.ToUpper();
            workString = workString.Replace(" ", "");
            string spacedString = "";
            for (int i = 0; i < workString.Length; i++)
            {
                if (!char.IsNumber(workString, i) && (workString[i] != 'A') && (workString[i] != 'B')
                   && (workString[i] != 'C') && (workString[i] != 'D') && (workString[i] != 'E') && (workString[i] != 'F'))
                { // non hex character
                    spacedString += '0';
                }
                else
                {
                    spacedString += workString[i];
                }
                if (((i + 1) % 2) == 0)
                {
                    spacedString += " ";
                }
            }
            if (spacedString.Length > MaxHexLength) 
            {
                spacedString = spacedString.Substring(0, MaxHexLength);
            }
            int selectSave = textBoxToFormat.SelectionStart;
            if ((selectSave > 0) && (selectSave <= spacedString.Length) && (selectSave < textBoxToFormat.Text.Length)
                && (textBoxToFormat.Text[selectSave] == ' ') && (spacedString[selectSave -1] == ' '))
            {
                selectSave ++;
            }
            else if ((selectSave >= textBoxToFormat.Text.Length) && (priorLength < textBoxToFormat.Text.Length))
            {
                selectSave = spacedString.Length;
            }
            textBoxToFormat.Text = spacedString;
            textBoxToFormat.SelectionStart = selectSave;
            priorLength = textBoxToFormat.Text.Length;
        }

        private void buttonString1_Click(object sender, EventArgs e)
        {
            sendString(textBoxString1.Text, checkBoxCRLF.Checked);
        }

        private void buttonString2_Click(object sender, EventArgs e)
        {
            sendString(textBoxString2.Text, checkBoxCRLF.Checked);
        }

        private void buttonString3_Click(object sender, EventArgs e)
        {
            sendString(textBoxString3.Text, checkBoxCRLF.Checked);
        }

        private void buttonString4_Click(object sender, EventArgs e)
        {
            sendString(textBoxString4.Text, checkBoxCRLF.Checked);
        }

        private void sendString(string dataString, bool appendCRLF)
        {
            if (dataString.Length == 0)
            {
                return;
            }
            if (radioButtonASCII.Checked)
            {
                if (appendCRLF)
                {
                    dataString += "\r\n";
                }
                if (checkBoxEcho.Checked)
                {
                    textBoxDisplay.AppendText(dataString);
                    textBoxDisplay.SelectionStart = textBoxDisplay.Text.Length;
                    textBoxDisplay.ScrollToCaret();
                }
                if (logFile != null)
                {
                    logFile.Write(dataString);
                }
                byte[] unicodeBytes = Encoding.Unicode.GetBytes(dataString);
                byte[] asciiBytes = Encoding.Convert(Encoding.Unicode, Encoding.ASCII, unicodeBytes);
                Pk2.DataDownload(asciiBytes, 0, asciiBytes.Length);
            }
            else
            {// hex data
                int numBytes = 0;
                if (dataString.Length > (MaxHexLength - 1))
                {
                    numBytes = ((MaxHexLength + 1) / 3);
                }
                else
                {
                    numBytes = dataString.Length / 3;
                    dataString = dataString.Substring(0, (numBytes * 3));
                }               
                byte[] hexBytes = new byte[numBytes];
                for (int i = 0; i < numBytes; i++)
                {
                    hexBytes[i] = (byte)Utilities.Convert_Value_To_Int("0x" + dataString.Substring((3 * i), 2));
                }
                dataString = "TX:  " + dataString + "\r\n";
                textBoxDisplay.AppendText(dataString);
                textBoxDisplay.SelectionStart = textBoxDisplay.Text.Length;
                textBoxDisplay.ScrollToCaret();
                if (logFile != null)
                {
                    logFile.Write(dataString);
                }
                Pk2.DataDownload(hexBytes, 0, hexBytes.Length);
            }
        }

        private void buttonLog_Click(object sender, EventArgs e)
        {
            if (logFile == null)
            {
                saveFileDialogLogFile.ShowDialog();
            }
            else
            {
                closeLogFile();
            }
        }
        
        private void closeLogFile()
        {
            logFile.Close();
            logFile = null;
            buttonLog.Text = "Log to File";
            buttonLog.BackColor = System.Drawing.SystemColors.ControlLight;
        }

        private void saveFileDialogLogFile_FileOk(object sender, CancelEventArgs e)
        {
            logFile = new StreamWriter(saveFileDialogLogFile.FileName);
            buttonLog.Text = "Logging Data...";
            buttonLog.BackColor = Color.Green;
        }

        private void radioButtonASCII_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonASCII.Checked)
            {
                checkBoxCRLF.Visible = true;
                checkBoxEcho.Enabled = true;
                labelTypeHex.Visible = false;
                labelTypeHex.Text = "Type Hex : ";
                labelMacros.Text = "String Macros:";
                textBoxString1.Text = convertHexSequenceToStringMacro(textBoxString1.Text);
                textBoxString2.Text = convertHexSequenceToStringMacro(textBoxString2.Text);
                textBoxString3.Text = convertHexSequenceToStringMacro(textBoxString3.Text);
                textBoxString4.Text = convertHexSequenceToStringMacro(textBoxString4.Text);
                if ((textBoxDisplay.Text.Length > 0) && (textBoxDisplay.Text[textBoxDisplay.Text.Length -1] != '\n'))
                {
                    textBoxDisplay.AppendText("\r\n");
                }
            }
        }

        private void radioButtonHex_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonHex.Checked)
            {
                checkBoxCRLF.Visible = false;
                checkBoxEcho.Enabled = false;
                labelTypeHex.Text = "Type Hex : ";
                labelTypeHex.Visible = false;
                labelMacros.Text = "Send Hex Sequences:";
                textBoxString1.Text = convertStringMacroToHexSequence(textBoxString1.Text);
                textBoxString2.Text = convertStringMacroToHexSequence(textBoxString2.Text);
                textBoxString3.Text = convertStringMacroToHexSequence(textBoxString3.Text);
                textBoxString4.Text = convertStringMacroToHexSequence(textBoxString4.Text);
                if ((textBoxDisplay.Text.Length > 0) && (textBoxDisplay.Text[textBoxDisplay.Text.Length - 1] != '\n'))
                {
                    textBoxDisplay.AppendText("\r\n");
                }
            }
        }
        
        private string convertHexSequenceToStringMacro(string hexSeq)
        {
            int numBytes = 0;
            if (hexSeq.Length > (MaxHexLength -1))
            {
                numBytes = ((MaxHexLength+1) / 3);
            }
            else
            {
                numBytes = hexSeq.Length / 3;
            }
            byte[] hexBytes = new byte[numBytes];
            for (int i = 0; i < numBytes; i++)
            {
                hexBytes[i] = (byte)Utilities.Convert_Value_To_Int("0x" + hexSeq.Substring((3 * i), 2));
            }

            return Encoding.ASCII.GetString(hexBytes, 0, hexBytes.Length);
        }
        
        private string convertStringMacroToHexSequence(string stringMacro)
        {
            if (stringMacro.Length > ((MaxHexLength + 1) / 3))
            {
                stringMacro = stringMacro.Substring(0, ((MaxHexLength + 1) / 3));
            }
            byte[] unicodeBytes = Encoding.Unicode.GetBytes(stringMacro);
            byte[] asciiBytes = Encoding.Convert(Encoding.Unicode, Encoding.ASCII, unicodeBytes);
            string hexSeq = "";
            for (int i = 0; i < asciiBytes.Length; i++)
            {
                hexSeq += string.Format("{0:X2} ", asciiBytes[i]);
            }
            return hexSeq;
        }

        private void checkBoxWrap_CheckedChanged(object sender, EventArgs e)
        {
            textBoxDisplay.WordWrap = checkBoxWrap.Checked;
        }

        private void comboBoxBaud_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxBaud.SelectedItem.ToString() == CUSTOM_BAUD)
            {
                DialogCustomBaud baudDialog = new DialogCustomBaud();
                baudDialog.ShowDialog();
                if (CustomBaud == "")
                {
                    comboBoxBaud.SelectedIndex = 0;
                }
                else
                {
                    if (comboBoxBaud.Items.Count != (comboBoxBaud.SelectedIndex + 1))
                    {// currently another custom value.
                        comboBoxBaud.Items.RemoveAt(comboBoxBaud.SelectedIndex + 1);
                    }
                    comboBoxBaud.Items.Add(CustomBaud);
                    comboBoxBaud.SelectedIndex += 1;
                }
            }
        }

        private void pictureBoxHelp_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(FormPICkit2.HomeDirectory + KONST.UserGuideFileNamePK2);
            }
            catch
            {
                MessageBox.Show("Unable to open User's Guide.");
            }
        }

        private void checkBoxVDD_Click(object sender, EventArgs e)
        {
            VddCallback(true, checkBoxVDD.Checked);
        }

        private void textBoxDisplay_Leave(object sender, EventArgs e)
        { // if the user clicks on something else, clear any pending type hex
            labelTypeHex.Visible = false;
            labelTypeHex.Text = "Type Hex : ";
        }



    }
}