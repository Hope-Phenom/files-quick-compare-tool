using MultithreadingScaffold;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace FilesQuickCompareTool
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// 以后谁再自己偷偷换了文件然后还乱报BUG，就把他头摁进显示器里
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string> FileList = new List<string>();
        private int MuiltThreadGain = 2;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 选择目录按钮事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Btn_SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new FolderBrowserDialog();
            fbd.ShowDialog();

            if (fbd.SelectedPath == null || fbd.SelectedPath == "")
                return;

            if (sender.Equals(Btn_SelectFolder))
                Tbx_FolderPath.Text = fbd.SelectedPath;
            else
                Tbx_FolderPath_2nd.Text = fbd.SelectedPath;
        }

        #region 基础功能模块

        /// <summary>
        /// 获取指定文件的MD5值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        private string GetFileMD5(string filePath)
        {
            try
            {
                FileStream file = new FileStream(filePath, FileMode.Open);
                System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
                var retVal = md5.ComputeHash(file);
                file.Close();

                var sb = new StringBuilder();
                for (int i = 0; i < retVal.Length; i++)
                    sb.Append(retVal[i].ToString("x2"));

                return sb.ToString();
            }
            catch (Exception ex)
            {
                PrintLog("GetMD5HashFromFile() fail, error: " + ex.Message);
                return "";
            }
        }

        /// <summary>
        /// 获取指定目录下所有文件的列表,方便后续进行多线程处理
        /// </summary>
        /// <param name="folderPath">目录路径</param>
        /// <returns></returns>
        private void GetFolderFileList(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return;

            var files = Directory.GetFiles(folderPath);
            if (files.Length > 0)
                foreach (var file in files)
                    FileList.Add(file);

            var dirs = Directory.GetDirectories(folderPath);
            if (dirs.Length > 0)
                foreach (var dir in dirs)
                    GetFolderFileList(dir);
        }

        /// <summary>
        /// 使用多线程计算之前处理好的文件列表
        /// </summary>
        /// <param name="basePath">基础路径，列表中的路径会删除此路径部分以获得相对路径</param>
        /// <returns></returns>
        private Dictionary<string, string> CalcFileList(string basePath, Action finalAction)
        {
            var dic = new Dictionary<string, string>();

            var mTScaffold = new MTScaffold();
            mTScaffold.Workload = FileList.Count;
            mTScaffold.ThreadLimit = Environment.ProcessorCount * MuiltThreadGain;
            mTScaffold.InNewThread = true;
            mTScaffold.Final =() =>
            {
                finalAction?.Invoke();
            };
            mTScaffold.Worker = (i) =>
            {
                var file = FileList[i];
                var md5 = GetFileMD5(file);

                lock (dic)
                    dic.Add(file.Replace(basePath, ""), md5);
            };
            mTScaffold.Start();

            return dic;
        }

        /// <summary>
        /// 比较两个数据源，并返回一个体现比对结果的DataTable
        /// </summary>
        /// <param name="source_1st">1号源</param>
        /// <param name="source_2nd">2号源</param>
        /// <param name="from_1st">1号源来源</param>
        /// <param name="from_2nd">2号源来源</param>
        /// <returns></returns>
        private DataTable CompareTwoSource(Dictionary<string, string> source_1st, Dictionary<string, string> source_2nd, string from_1st, string from_2nd)
        {
            var dt = new DataTable();
            dt.Columns.Add("结论");
            dt.Columns.Add("文件名");
            dt.Columns.Add("来源");
            dt.Columns.Add("MD5值");

            foreach (var kv in source_1st)
                if (!source_2nd.ContainsKey(kv.Key))
                    dt.Rows.Add("特有", kv.Key, from_1st, kv.Value);
                else if (source_2nd[kv.Key] != source_1st[kv.Key])
                    dt.Rows.Add("不同", kv.Key, "两者", kv.Value);

            foreach (var kv in source_2nd)
                if (!source_1st.ContainsKey(kv.Key))
                    dt.Rows.Add("特有", kv.Key, from_2nd, kv.Value);

            return dt;
        }

        /// <summary>
        /// 在日志区写入日志信息
        /// </summary>
        /// <param name="log">日志内容</param>
        private void PrintLog(string log)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var info = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {log}";
                Tbx_Info.Text += info + Environment.NewLine;
                Tbx_Info.ScrollToEnd();
            }));
        }

        #endregion

        #region UI组件事件

        /// <summary>
        /// 生成比较列表清单事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Btn_MakeCompareList_Click(object sender, RoutedEventArgs e)
        {
            ChangeButtons(false);
            PrintLog("开始生成比较列表清单……");

            await Task.Run(() =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        FileList.Clear();
                        var currTime = DateTime.Now;

                        var sfd = new SaveFileDialog();
                        sfd.Filter = "文本文件(.txt)|*.txt";
                        sfd.ShowDialog();

                        if (sfd.FileName == null || sfd.FileName == "")
                            return;

                        GetFolderFileList(Tbx_FolderPath.Text);
                        var dict = new Dictionary<string, string>();
                        dict = CalcFileList(Tbx_FolderPath.Text, new Action(() =>
                        {
                            var ls = new List<string>();
                            foreach (var keyValue in dict)
                                ls.Add($@"{keyValue.Key},{keyValue.Value}");

                            File.WriteAllLines(sfd.FileName, ls.ToArray());

                            PrintLog($@"列表生成完毕，共计计算{ls.Count}个文件，耗时{Math.Round((DateTime.Now - currTime).TotalSeconds)}秒！");
                            ChangeButtons(true);
                        }));
                    }
                    catch (Exception ex)
                    {
                        PrintLog($@"生成比较清单异常，{ex.Message}");
                    }
                }));
            });
        }

        /// <summary>
        /// 修改多线程倍率参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                MuiltThreadGain = Convert.ToInt32(Tbx_MTGain.Text);
            }
            catch (Exception)
            {

            }
        }

        /// <summary>
        /// 比较两个目录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Btn_CompareTwoFolder_Click(object sender, RoutedEventArgs e)
        {
            ChangeButtons(false);
            PrintLog("开始比较两个目录……");

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (Tbx_FolderPath.Text == "" || Tbx_FolderPath_2nd.Text == "")
                        return;

                    var currTime = DateTime.Now;

                    FileList.Clear();
                    GetFolderFileList(Tbx_FolderPath.Text);
                    var dict1 = new Dictionary<string, string>();
                    dict1 = CalcFileList(Tbx_FolderPath.Text, new Action(() =>
                    {
                        Dispatcher.BeginInvoke((Action)(() =>
                        {
                            FileList.Clear();
                            GetFolderFileList(Tbx_FolderPath_2nd.Text);
                            var dict2 = new Dictionary<string, string>();
                            dict2 = CalcFileList(Tbx_FolderPath_2nd.Text, new Action(() =>
                            {
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    var dt = CompareTwoSource(dict1, dict2, Tbx_FolderPath.Text, Tbx_FolderPath_2nd.Text);

                                    var endTime = DateTime.Now;

                                    dataGrid_Main.ItemsSource = dt.DefaultView;
                                    PrintLog($@"目录比对完成，共计比对{dict1.Count + dict2.Count}个文件，耗时{Math.Round((endTime - currTime).TotalSeconds)}秒！");
                                    ChangeButtons(true);
                                }));
                            }));
                        }));
                    }));
                }
                catch (Exception ex)
                {
                    PrintLog($@"比较目录异常，{ex.Message}");
                }
            }));
        }

        /// <summary>
        /// 从清单比较
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Btn_CompareFromList_Click(object sender, RoutedEventArgs e)
        {
            ChangeButtons(false);
            PrintLog("开始从清单比较……");

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var ofd = new OpenFileDialog();
                    ofd.Title = "请选择一个清单文件...";
                    ofd.Filter = "清单文件|*.txt";
                    ofd.ShowDialog();

                    if (ofd.FileName == null || ofd.FileName == "" || Tbx_FolderPath.Text == "")
                        return;

                    var currTime = DateTime.Now;

                    var arr = File.ReadAllLines(ofd.FileName);
                    var dict2 = new Dictionary<string, string>();
                    foreach (var line in arr)
                    {
                        var _arr = line.Split(',');
                        dict2.Add(_arr[0], _arr[1]);
                    }

                    FileList.Clear();
                    GetFolderFileList(Tbx_FolderPath.Text);
                    var dict1 = new Dictionary<string, string>();
                    CalcFileList(Tbx_FolderPath.Text, new Action(() =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var dt = CompareTwoSource(dict1, dict2, Tbx_FolderPath.Text, $@"清单文件:{ofd.FileName}");

                            var endTime = DateTime.Now;

                            dataGrid_Main.ItemsSource = dt.DefaultView;
                            PrintLog($@"目录比对完成，共计比对{dict1.Count + dict2.Count}个文件，耗时{Math.Round((endTime - currTime).TotalSeconds)}秒！");
                            ChangeButtons(true);
                        }));
                    }));
                }
                catch (Exception ex)
                {
                    PrintLog($@"从清单比较异常，{ex.Message}");
                }
            }));
        }

        /// <summary>
        /// 比较两个清单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Btn_CompareTwoList_Click(object sender, RoutedEventArgs e)
        {
            ChangeButtons(false);
            PrintLog("开始从比较两个清单……");

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    var ofd = new OpenFileDialog();
                    ofd.Title = "请选择第一个清单文件...";
                    ofd.Filter = "清单文件|*.txt";
                    ofd.ShowDialog();

                    var ofd_2nd = new OpenFileDialog();
                    ofd_2nd.Title = "请选择第二个清单文件...";
                    ofd_2nd.Filter = "清单文件|*.txt";
                    ofd_2nd.ShowDialog();

                    if (ofd.FileName == null || ofd.FileName == "" || ofd_2nd.FileName == null || ofd_2nd.FileName == "")
                        return;

                    var currTime = DateTime.Now;

                    var arr = File.ReadAllLines(ofd.FileName);
                    var dict1 = new Dictionary<string, string>();
                    foreach (var line in arr)
                    {
                        var _arr = line.Split(',');
                        dict1.Add(_arr[0], _arr[1]);
                    }

                    var arr2 = File.ReadAllLines(ofd_2nd.FileName);
                    var dict2 = new Dictionary<string, string>();
                    foreach (var line in arr2)
                    {
                        var _arr = line.Split(',');
                        dict2.Add(_arr[0], _arr[1]);
                    }

                    var dt = CompareTwoSource(dict1, dict2, $@"清单文件:{ofd.FileName}", $@"清单文件:{ofd_2nd.FileName}");

                    var endTime = DateTime.Now;

                    dataGrid_Main.ItemsSource = dt.DefaultView;
                    PrintLog($@"目录比对完成，共计比对{dict1.Count + dict2.Count}个文件，耗时{Math.Round((endTime - currTime).TotalSeconds)}秒！");
                    ChangeButtons(true);
                }));
            }));
        }

        /// <summary>
        /// 改变按钮状态
        /// </summary>
        /// <param name="enable">可用性</param>
        private void ChangeButtons(bool enable)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Btn_CompareFromList.IsEnabled =
                    Btn_CompareTwoFolder.IsEnabled =
                    Btn_CompareTwoList.IsEnabled =
                    Btn_MakeCompareList.IsEnabled = enable;
            }));
        }

        #endregion
    }
}
