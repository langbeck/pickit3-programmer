using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace PICkit2V2
{
    public partial class DialogCustomBaud : Form
    {
        public DialogCustomBaud()
        {
            InitializeComponent();
            textBox1.Focus();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (textBox1.Text.Length > 0)
            {
                if (!char.IsDigit(textBox1.Text[textBox1.Text.Length-1]))
                {
                    textBox1.Text = textBox1.Text.Substring(0, (textBox1.Text.Length-1));
                }
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            try
            {
                int baud = int.Parse(textBox1.Text);
                if ((baud < 150) || (baud > 38400))
                {
                    MessageBox.Show("Baud value is outside\nthe Min / Max range.");
                }
                else
                {
                    DialogUART.CustomBaud = textBox1.Text;
                    this.Close();
                }
            }
            catch
            {
                MessageBox.Show("Illegal Value.");
            }
        }
    }
}