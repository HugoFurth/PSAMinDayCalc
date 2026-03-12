using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PSAMinDay
    {
    static class Program
        {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
            {
            PSAMinDayContext t;
            t= null;
            try
                {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                t =  new PSAMinDayContext();
                Application.Run(t);
                }
            catch (Exception ee)
                {
                MessageBox.Show(ee.Message);
                }
            finally
                {
                if (t != null)
                    t.WriteLogInfoOnly("Application terminated");
                }
            }
        }
    }
