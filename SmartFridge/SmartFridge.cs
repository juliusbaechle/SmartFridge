﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SmartFridge {
    class SmartFridge {
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            SmartFridge fridge = new SmartFridge();
            Application.Run(fridge.CreateDesktopView());
        }

        Form CreateDesktopView()
        {
            return new ProductForm();
        }
    }
}
