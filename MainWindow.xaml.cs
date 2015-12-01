using System.Windows;
using Db4objects.Db4o;
using Db4objects.Db4o.Ext;
using System.ComponentModel;
using System;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Text;
using System.Reflection;

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
        private string originFile = null;
        private string assemblyFile = null;

        private BackgroundWorker worker;

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
                if (db != null)
                {
                    db.Close();
                }

                originFile = dlg.FileName;
                tbFilePath.Text = originFile;
                db = Db4oFactory.OpenFile(originFile);
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
                //if (c.GetName().StartsWith("Unionless"))
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
                    foreach(var cf in clzList)
                    {
                        if (f.GetStoredType().GetName() == cf.GetName())
                        {
                            ListFields(f.Get(o), cf, ++depth, f.GetName());
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
            if (originFile == null || assemblyFile == null)
            {
                MessageBox.Show("Both original db4o database file and assembly files should be set!");
                return;
            }

            SaveFileDialog dlg = new SaveFileDialog();
            dlg.DefaultExt = ".yap";
            dlg.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
            dlg.Filter = "db4o files (*.yap)|*.yap";
            dlg.FileName = "db4o-" + DateTime.Now.Ticks + ".yap";
            if ((bool) dlg.ShowDialog())
            {
                tbNewFilePath.Text = dlg.FileName;
                ConvertToNew(db, tbNewFilePath.Text);
                Msg(string.Format("Started to convert from {0} to {1}...\n", db, tbNewFilePath.Text));
            }
        }

        private void ConvertToNew(IObjectContainer db1, string f2)
        {
            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
            worker.ProgressChanged += Worker_ProgressChanged;

            worker.DoWork += (sender, e) =>
            {
                IObjectContainer db2 = Db4oFactory.OpenFile(f2);

                var clist = db1.Ext().StoredClasses();
                worker.ReportProgress(0, string.Format("{0} classes are listed.", clist.Length));

                Assembly assembly = Assembly.LoadFile(assemblyFile);
                worker.ReportProgress(0, string.Format("Assembly [{0}] is loaded.", assemblyFile));
                foreach(Type t in assembly.GetTypes())
                {
                    worker.ReportProgress(0, string.Format("++Type [{0}] is loaded.", t));
                }

                foreach (IStoredClass c in clist)
                {
                    // try to list objects.
                    var ids = c.GetIDs();
                    worker.ReportProgress(0, string.Format("{0} objects for class [{1}] are listed.", ids.Length, c.GetName()));

                    foreach(var id in ids)
                    {
                        var o = db1.Ext().GetByID(id);
                        var o2 = PopulateFrom(id, o, c, assembly);
                        if (o2 != null)
                        {
                            db2.Store(o2);
                        }
                    }
                }

                db2.Commit();
                db2.Close();
            };

            worker.RunWorkerAsync();
        }

        private object PopulateFrom(long id, object o, IStoredClass c, Assembly assembly)
        {
            try
            {
                string cname = c.GetName();
                int pos = cname.IndexOf(',');
                if (pos > 0)
                {
                    cname = cname.Substring(0, pos);
                }
                var o2 = assembly.CreateInstance(cname);
                worker.ReportProgress(0, string.Format("++{0} - {1} of class [{2}] is converted to {3}.",
                    id, o, cname, o2));

                if (o2 != null)
                {
                    DeepCopy(id, o, c, o2, assembly);
                }

                return o2;
            }
            catch (Exception ex)
            {
                worker.ReportProgress(0, string.Format("FAIL to convert from {0} - {1} of class [{2}] caused by {3}",
                    id, o, c.GetName(), ex.Message));
                return null;
            }
        }

        /// <summary>
        /// Deep copy from db4o object to o2.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="o"></param>
        /// <param name="c"></param>
        /// <param name="o2"></param>
        /// <param name="assembly"></param>
        private void DeepCopy(long id, object o, IStoredClass c, object o2, Assembly assembly)
        {
            foreach(IStoredField f in c.GetStoredFields())
            {
                var v = f.Get(o);
                var f2 = o2.GetType().GetField(f.GetName(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance | BindingFlags.Instance);

                if (f2 != null)
                {
                    if (f.GetStoredType().IsImmutable())
                    {
                        f2.SetValue(o2, v);
                    }
                    else
                    {
                        // create reflectively.
                        string cfname = f.GetStoredType().GetName();
                        int pos = cfname.IndexOf(',');
                        if (pos > 0)
                        {
                            cfname = cfname.Substring(0, pos);
                        }
                        var o3 = assembly.CreateInstance(cfname);

                        // copy fields.
                        foreach (var cf in clzList)
                        {
                            if (f.GetStoredType().GetName() == cf.GetName())
                            {
                                DeepCopy(id, v, cf, o3, assembly);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    worker.ReportProgress(0, string.Format("FAIL to find field [{0}] of class [{1}]", f.GetName(), o2.GetType()));
                }
            }
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string v = "" + e.UserState + "\n";
            tbMsg.AppendText(v);
        }

        #endregion

        private void btnLoadAssembly_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.DefaultExt = ".dll";
            dlg.Filter = "assembly files (*.dll;*.exe)|*.dll;*.exe";
            dlg.Multiselect = true;
            dlg.Title = "Load Assemblies";
            if ((bool) dlg.ShowDialog())
            {
                assemblyFile = "";
                foreach (var fn in dlg.FileNames)
                {
                    assemblyFile = fn;
                }
                tbAssemblyFilePath.Text = assemblyFile;
            }
        }
    }
}
