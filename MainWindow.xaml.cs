using System.Windows;
using Db4objects.Db4o;
using Db4objects.Db4o.Ext;
using System.ComponentModel;
using System;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Text;

namespace ObjectTran
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<IStoredClass> clzList = new ObservableCollection<IStoredClass>();
        private ObservableCollection<Object> objList = new ObservableCollection<object>();

        private IObjectContainer db;

        public MainWindow()
        {
            InitializeComponent();
            tbStatus.Text += "  Version: " + Db4oViewer.Properties.Resources.CurVersion;
        }

        private void btnOpenDb4o_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.DefaultExt = ".yap";
            dlg.Filter = "db4o file (*.yap)|*.yap";
            dlg.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if ((bool) dlg.ShowDialog())
            {
                tbFilePath.Text = dlg.FileName;
                db = Db4oFactory.OpenFile(tbFilePath.Text);
                var ext = db.Ext();

                PopulateCombos(db);
            }
        }

        private void PopulateCombos(IObjectContainer db)
        {
            comboType.Items.Clear();
            clzList.Clear();
            var clzs = db.Ext().StoredClasses();
            foreach (var c in clzs)
            {
                if (c.GetName().StartsWith("Unionless"))
                {
                    comboType.Items.Add(c.GetName() + " - " + c.GetIDs().Length);
                    clzList.Add(c);
                }
            }

            Msg(string.Format("{0} classes are listed.\n", clzs.Length));
        }

        private void Msg(string v)
        {
            tbMsg.AppendText(v);
        }

        private void comboType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (comboType.SelectedIndex < 0)
            {
                return;
            }

            comboIds.Items.Clear();
            objList.Clear();

            IStoredClass c = clzList[comboType.SelectedIndex];
            var ids = c.GetIDs();
            foreach (var id in ids)
            {
                var o = db.Ext().GetByID(id);
                objList.Add(o);
                comboIds.Items.Add(string.Format("{0} - {1}", id, o));
            }

            Msg(string.Format("{0} objects for class [{1}] are listed.\n", ids.Length, c.GetName()));
        }

        private void comboIds_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var idx = comboIds.SelectedIndex;
            if (idx < 0)
            {
                return;
            }

            var o = objList[idx];
            var c = clzList[comboType.SelectedIndex];

            Msg(string.Format("\n{0}=>\n", comboIds.SelectedValue));
            if ((bool) rbOutputFormat.IsChecked)
            {
                Msg(JsonConvert.SerializeObject(o, Formatting.Indented));
            }
            else
            {
                ListFields(o, c);
            }

            Msg("\n===============END==============\n");
        }

        private void ListFields(object o, IStoredClass c, int depth = 0, string fname = "")
        {
            string pad = "";

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < depth; i++)
            {
                sb.Append('\t');
            }
            pad = sb.ToString();

            // output field firstly.
            if (depth > 0)
            {
                Msg(string.Format("{0}Field [{1}] of class [{2}] =>\n", pad, fname, c.GetName()));
            }

            pad += "\t";

            foreach (var f in c.GetStoredFields())
            {
                if (f.GetStoredType().IsImmutable())
                {
                    object v = null;
                    try
                    {
                        v = f.Get(o);
                    }
                    catch(Exception ex)
                    {
                        // nothing.
                    }
                    Msg(string.Format("{0}{1} of type[{2}] = {3}.\n", pad, f.GetName(), f.GetStoredType(), v));
                }
                else
                {
                    foreach(var fc in clzList)
                    {
                        if (f.GetStoredType().GetName() == fc.GetName())
                        {
                            ListFields(f.Get(o), fc, ++depth, f.GetName());
                            break;
                        }
                    }
                }
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            tbMsg.Clear();
        }

        private void btnCut_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(tbMsg.Text);
            tbMsg.Clear();
        }

        #region convertion function
        private void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            string fname = "data\\db4o\\db-" + DateTime.Now.Ticks + ".yap";
            string f1 = "data\\db4o\\mis222.yap";
            ConvertToNew(f1, fname);
            Msg(string.Format("Converted from {0} to {1} successfully.\n", f1, fname));
        }

        private void ConvertToNew(string f1, string f2)
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
            worker.ProgressChanged += Worker_ProgressChanged;

            worker.DoWork += (sender, e) =>
            {
                IObjectContainer db1 = Db4oFactory.OpenFile(f1);
                IObjectContainer db2 = Db4oFactory.OpenFile(f2);

                var clist = db1.Ext().StoredClasses();
                worker.ReportProgress(0, string.Format("{0} classes are listed.", clist.Length));
                foreach (IStoredClass c in clist)
                {
                    if (!c.GetName().StartsWith("Unionless"))
                    {
                        continue;
                    }

                    // try to list objects.
                    var ids = c.GetIDs();
                    worker.ReportProgress(0, string.Format("{0} objects for class {1}.", ids.Length, c.GetName()));

                    foreach(var id in ids)
                    {
                        var o = db1.Ext().GetByID(id);
                        var o2 = PopulateFrom(o, c);
                    }
                }
            };

            worker.RunWorkerAsync();
        }

        private object PopulateFrom(object o, IStoredClass c)
        {
            Type t = Type.GetType(c.GetName());
            return null;
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string v = "" + e.UserState + "\n";
            tbMsg.AppendText(v);
        }

        #endregion
    }
}
