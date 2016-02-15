﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using ScriptCore;

namespace SSE
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();                    
        }
        
        #region Properties
        private MySettings Settings;
        private ScriptManager _sm;
        private List<MyCheckBox> _checkBoxList;
        private NotifyIcon _trayIcon;
        private string _scriptsFolder = AppDomain.CurrentDomain.BaseDirectory + "Scripts\\";
        private Task mainLoop;
        private CancellationTokenSource token;
        #endregion

        #region Form Events
        private void MainForm_Load(object sender, EventArgs e)
        {
            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "Simple Script Environment";
            _trayIcon.ContextMenu = new ContextMenu();
            _trayIcon.ContextMenu.MenuItems.Add(new MenuItem("Exit", new EventHandler(trayExitClick)));
            _trayIcon.Click += trayOpenClick;
            _trayIcon.Visible = true;
            _trayIcon.Icon = this.Icon;            
            _sm = new ScriptManager();
            _checkBoxList = new List<MyCheckBox>();
            if (!Directory.Exists(_scriptsFolder))
                Directory.CreateDirectory(_scriptsFolder);
            foreach (string file in Directory.GetFiles(_scriptsFolder, "*.cs"))
            {
                AddScript(file, true);
            }
            foreach (string directory in Directory.GetDirectories(_scriptsFolder))
            {
                AddScript(directory, true);
            }
            foreach (var item in _sm.Scripts)
            {
                var a = new MenuItem[] { };
                var box = new MyCheckBox();
                box.Tag = item.ScriptName;
                box.Text = item.ScriptName;
                box.Script = item;
                box.Checked = true;
                box.AutoSize = true;
                box.CheckedChanged += OnCheckedChanged;
                var myMenuItem = new MyMenuItem(item.ScriptName, new MenuItem[]
                    {
                        new MenuItem(item.Enabled?"Disable":"Enable", new EventHandler(trayScriptEnableClick)),
                        new MenuItem("Action", new EventHandler(trayScriptActionClick))
                    });
                myMenuItem.Script = item;                  
                    _trayIcon.ContextMenu.MenuItems.Add(myMenuItem);
                
                AddToPanel(box);
            }

            LoadSettings(null);

            if (Settings.minimizedStart)
                this.WindowState = FormWindowState.Minimized;

            RepopulatePanel();

            StartLoop();
        }

        private void StartLoop()
        {
            token = new CancellationTokenSource();
            var canellationToken = token.Token;
            mainLoop = Task.Factory.StartNew(() =>
            {
                canellationToken.ThrowIfCancellationRequested();
                _sm.Execute();
                Thread.Sleep(50);
            }, canellationToken);
        }

        private void StopLoop()
        {
            token.Cancel();
            try
            {
                mainLoop.Wait();
            }
            catch (AggregateException)
            { }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            //RegisterInStartup(Settings.runOnWinStart);
            _trayIcon.Visible = false;
        }
        
        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                this.ShowInTaskbar = false;
            }
        }

        private void panel1_DragDrop(object sender, DragEventArgs e)
        {
            var s = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            if (s != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    AddScript(s[i], false);
                }
                RepopulatePanel();
            }
        }

        private void panel1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SettingsForm stfrm = new SettingsForm();
            stfrm.settings = this.Settings;
            if (stfrm.ShowDialog() == DialogResult.OK)
            {
                var settings = stfrm.GetSettings();
                LoadSettings(settings);
            }
        }
        private void fromFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var browser = new FolderBrowserDialog();
            browser.Description = "Select Script folder";
            if (browser.ShowDialog() == DialogResult.OK)
            {
                AddScript(browser.SelectedPath, false);
            }
        }

        private void fromFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var browser = new OpenFileDialog();
            browser.Title = "Select Script file";
            browser.Filter = "CS files|*.cs";
            if (browser.ShowDialog() == DialogResult.OK)
            {
                string path = Path.GetDirectoryName(browser.FileName) + "\\" + browser.SafeFileName;
                AddScript(path, false);
            }
        }
        
        #endregion

        #region Panel Management

        /// <summary>
        /// Redraws the panel with checkboxes
        /// </summary>
        private void RepopulatePanel()
        {
            panel1.Controls.Clear();
            _checkBoxList.Clear();
            foreach (var item in _sm.Scripts)
            {
                var box = new MyCheckBox();
                box.Tag = item.ScriptName;
                box.Text = item.ScriptName;
                box.Script = item;
                box.Checked = item.Enabled;
                box.AutoSize = true;
                box.CheckedChanged += OnCheckedChanged;
                AddToPanel(box);
            }
        }

        /// <summary>
        /// Refreshes script run parameter in the panel checkboxes
        /// </summary>
        private void RefreshPanel()
        {
            //panel1.Controls.Clear();
            foreach (MyCheckBox item in panel1.Controls)
            {
                item.Checked = item.Script.Enabled;
            }
        }

        /// <summary>
        /// Adds a checkbox to the panel
        /// </summary>
        /// <param name="box">MyCheckBox object</param>
        private void AddToPanel(MyCheckBox box)
        {
            int i = 1;
            if (_checkBoxList.Count > 0)
                i += _checkBoxList[_checkBoxList.Count - 1].Location.Y / 20;
            box.Location = new Point(20, i * 20);
            _checkBoxList.Add(box);
            panel1.Controls.Add(box);
        }
        #endregion

        #region My Events

        private void trayExitClick(object sender, EventArgs e)
        {
            this.Close();
        }

        private void trayOpenClick(object sender, EventArgs e)
        {
            if (this.WindowState != FormWindowState.Normal)
            {
                this.Show();
                this.ShowInTaskbar = true;
                this.WindowState = FormWindowState.Normal;
            }
            else
            {
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
                this.ShowInTaskbar = false;
            }
        }
        private void trayScriptEnableClick(object sender, EventArgs e)
        {
            var menuItem = (MenuItem)sender;
            var itemParent = (MyMenuItem)menuItem.Parent;

            if (itemParent.Script.Enabled)
            {
                itemParent.Script.Enabled = false;
                menuItem.Text = "Enable";                
            }
            else
            {
                itemParent.Script.Enabled = true;
                menuItem.Text = "Disable";
            }
            //sot he form won't hide itself
            //this is retarded
            this.Show();
            this.ShowInTaskbar = true;
            this.WindowState = FormWindowState.Normal;
            RefreshPanel();
        }
        private void trayScriptActionClick(object sender, EventArgs e)
        {
            var menuItem = (MenuItem)sender;
            var itemParent = (MyMenuItem)menuItem.Parent;
            itemParent.Script.Action();
        }

        private void OnCheckedChanged(object sender, EventArgs e)
        {
            MyCheckBox box = (MyCheckBox)sender;
            box.Script.Enabled = box.Checked;
            box.Refresh();/*
            foreach (var item in _checkBoxList)
            {
                if (item.Script.FileName == box.Script.FileName)
                {
                    item.Script.Enabled = box.Checked;
                }
                item.Refresh();
            }
            foreach (var item in _sm.Scripts)
            {
                if (item.FileName == box.Script.FileName)
                {
                    item.Enabled = box.Checked;
                }
            }
            //RefreshPanel();*/
        }

        #endregion

        #region Methods

        /// <summary>
        /// Register for autostart on windows startup
        /// </summary>
        /// <param name="isChecked"></param>
        public static void RegisterInStartup(bool isChecked)
        {
            /*RegistryKey registryKey = Registry.CurrentUser.OpenSubKey
                    ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (isChecked)
            {
                registryKey.SetValue("SSE", Application.ExecutablePath);
            }
            else
            {
                registryKey.DeleteValue("SSE");
            }*/
        }

        private void LoadSettings(MySettings settings)
        {
            
            if (settings == null)
                this.Settings = MySettings.Load();
            else
                this.Settings = settings;
        }

        private void AddScript(string text, bool k)
        {
            if (Directory.Exists(text))
            {
                bool copied = false;
                foreach (string file in Directory.GetFiles(text, "*.cs", SearchOption.AllDirectories))
                {
                    FileInfo fi = new FileInfo(file);
                    if (fi.Extension == ".cs")
                    {
                        string message;
                        if (!_sm.Add(fi.FullName, k, out message))
                            richTextBox1.Text += message + '\n';
                        else
                        {
                            if (!copied && !Directory.Exists(_scriptsFolder+(new DirectoryInfo(text)).Name))
                            {
                                DirectoryCopy(text, _scriptsFolder);
                                copied = true;
                            }
                        }
                    }
                }
            }
            else
            {
                FileInfo fi = new FileInfo(text);
                if (fi.Extension == ".cs")
                {
                    string message;
                    if (!_sm.Add(fi.FullName, k, out message))
                        richTextBox1.Text += message + '\n';
                    else
                    {
                        if (!File.Exists(_scriptsFolder + fi.Name))
                            File.Copy(text, _scriptsFolder + fi.Name, true);
                    }
                }
            }
            RepopulatePanel();
        }

        /// <summary>
        /// Copies recursively source directory to destination
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <param name="targetDirectory"></param>
        public void DirectoryCopy(string sourceDirectory, string targetDirectory)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);
            DirectoryInfo diTarget = new DirectoryInfo(targetDirectory+ diSource.Name);

            CopyAll(diSource, diTarget);
        }

        public void CopyAll(DirectoryInfo source, DirectoryInfo target)
        {
            // Check if the target directory exists; if not, create it.
            if (Directory.Exists(target.FullName) == false)
            {
                Directory.CreateDirectory(target.FullName);
            }

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }

        #endregion
        

    }
}