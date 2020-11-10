using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DevExpress.UserSkins;
using DevExpress.Skins;
using DevExpress.LookAndFeel;
using System.Numerics;

namespace SavixSim
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            int a = 20;
            int b = 11;
            int x = a / b;


            BigInteger highNum = BigInteger.Parse("100000000000000");

            Int64 lowNum = Convert.ToInt64((decimal) highNum / Convert.ToDecimal(Math.Pow(10, 9)));

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm form = new MainForm();
            BonusSkins.Register();
            Application.Run(form);
        }
    }
}
