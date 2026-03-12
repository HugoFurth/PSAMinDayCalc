using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PSAMinDayCalcViewer
    {
    static class Program
        {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
            {
            try
                {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
                }
            catch (Exception ee)
                {
                String InnerMess = "";
                if (ee.InnerException != null)
                    InnerMess = " / " + ee.InnerException.Message;
                MessageBox.Show(ee.Message + " " + InnerMess);
                }
            }
        }
    }
